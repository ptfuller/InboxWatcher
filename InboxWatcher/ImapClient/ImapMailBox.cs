﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.DTO;
using InboxWatcher.Enum;
using InboxWatcher.Interface;
using InboxWatcher.Notifications;
using InboxWatcher.WebAPI.Controllers;
using MailKit;
using MailKit.Search;
using Microsoft.AspNet.SignalR;
using MimeKit;
using NLog;
using WebGrease.Css.Extensions;

namespace InboxWatcher.ImapClient
{
    public class ImapMailBox
    {
        private ImapIdler _imapIdler;
        private ImapWorker _imapWorker;
        private EmailSender _emailSender;
        private EmailFilterer _emailFilterer;

        private readonly MailBoxLogger _mbLogger;
        private readonly IClientConfiguration _config;
        private readonly ImapClientDirector _imapClientDirector;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private bool _setupInProgress { get; set; }

        private List<AbstractNotification> NotificationActions = new List<AbstractNotification>();

        public IEnumerable<IMailFolder> EmailFolders { get; set; } = new List<IMailFolder>();
        public List<IMessageSummary> EmailList { get; set; } = new List<IMessageSummary>();
        public List<Exception> Exceptions = new List<Exception>();

        public string MailBoxName { get; private set; }

        public DateTime WorkerStartTime { get; private set; } 
        public DateTime IdlerStartTime { get; private set; }

        public event EventHandler NewMessageReceived;
        public event EventHandler MessageRemoved;

        public ImapMailBox(ImapClientDirector icd, IEnumerable<AbstractNotification> notificationActions, IClientConfiguration config) :
                this(icd, config)
        {
            NotificationActions.AddRange(notificationActions);
        }
        
        public ImapMailBox(ImapClientDirector icd, AbstractNotification notificationAction, IClientConfiguration config) :
            this(icd, config)
        {
            NotificationActions.Add(notificationAction);
        }

        public ImapMailBox(ImapClientDirector icd, IClientConfiguration config)
        {
            _imapClientDirector = icd;
            _config = config;
            MailBoxName = _config.MailBoxName;
            _mbLogger = new MailBoxLogger(_config);

            Setup();
        }


        public virtual async Task Setup()
        {
            if (_setupInProgress) return;

            _setupInProgress = true;

            int retryTime = 5000;

            //if setup fails let's try again soon
            while (!SetupClients())
            {
                Debug.WriteLine($"{MailBoxName} SetupClients failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160)
                {
                    retryTime = retryTime*2;
                }
            }

            Debug.WriteLine(MailBoxName + " SetupClients finished");

            retryTime = 5000;

            //make worker get initial list of messages and then start idling
            while (!FreshenMailBox())
            {
                Debug.WriteLine($"{MailBoxName} FreshenMailBox failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160)
                {
                    retryTime = retryTime * 2;
                }
            }
            
            Debug.WriteLine($"{MailBoxName} FreshMailBox finished");

            //get folders
            EmailFolders = _imapIdler.GetMailFolders();

            //setup email filterer
            _emailFilterer = new EmailFilterer(this);

            //filter all new messages
            Task.Factory.StartNew(() => _emailFilterer.FilterAllMessages(EmailList));

            Exceptions.Clear();
            _setupInProgress = false;
        }

        private void ImapClientExceptionHappened(object sender, InboxWatcherArgs args)
        {
            var ex = (Exception) sender;
            var exception = new Exception(DateTime.Now.ToString(), ex);

            Exceptions.Add(exception);

            if (args.NeedReset)
            {
                _imapWorker = null;
                _imapIdler = null;

                Setup();
            }
        }

        public MailBoxStatusDto Status()
        {
            try
            {
                var status = new MailBoxStatusDto()
                {
                    Exceptions = Exceptions,
                    IdlerConnected = _imapIdler.IsConnected(),
                    StartTime = IdlerStartTime.ToLocalTime().ToString("f"),
                    IdlerIdle = _imapIdler.IsIdle(),
                    WorkerConnected = _imapWorker.IsConnected(),
                    WorkerIdle = _imapWorker.IsIdle(),
                    SmtpConnected = _emailSender.IsConnected(),
                    SmtpAuthenticated = _emailSender.IsAuthenticated(),
                    Green = _imapIdler.IsConnected() && _imapIdler.IsIdle() && _imapWorker.IsConnected() && _emailSender.IsAuthenticated()
                };

                return status;
            }
            catch (Exception ex)
            {
                var status = new MailBoxStatusDto()
                {
                    Exceptions = Exceptions,
                    IdlerConnected = false,
                    StartTime = "Not Started",
                    Green = false,
                    IdlerIdle = false,
                    WorkerIdle = false,
                    WorkerConnected = false
                };

                if (ex is NullReferenceException)
                return status;

                Exceptions.Add(ex);

                return status;
            }
        }

        public void Destroy()
        {
            try
            {
                _imapIdler.Destroy();
                _imapWorker.Destroy();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private bool SetupClients()
        {
            try
            {
                _imapWorker = new ImapWorker(_imapClientDirector);
                WorkerStartTime = DateTime.Now;
                _imapIdler = new ImapIdler(_imapClientDirector);
                IdlerStartTime = DateTime.Now;

                _emailSender = new EmailSender(_imapClientDirector);

                _emailSender.ExceptionHappened += EmailSenderOnExceptionHappened;

                //exceptions that happen in the idler or worker get logged in a list in the mailbox
                _imapIdler.ExceptionHappened -= ImapClientExceptionHappened;
                _imapIdler.ExceptionHappened += ImapClientExceptionHappened;

                _imapWorker.ExceptionHappened -= ImapClientExceptionHappened;
                _imapWorker.ExceptionHappened += ImapClientExceptionHappened;

                _emailSender.Setup();
                _imapIdler.Setup();
                _imapWorker.Setup();

                //setup event handlers
                _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
                _imapIdler.MessageExpunged += ImapIdlerOnMessageExpunged;
                _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;
            }
            catch (Exception ex)
            {
                var exception = new Exception("Client setup failed.  Verify client settings and check inner exceptions for more information.", ex);
                Exceptions.Add(exception);
                return false;
            }

            return true;
        }

        private void EmailSenderOnExceptionHappened(object sender, InboxWatcherArgs inboxWatcherArgs)
        {
            var exception = new Exception(DateTime.Now.ToString(),(Exception) sender);
            Exceptions.Add(exception);
            logger.Info("Exception happened with Email Sender - Creating a new email sender");
            _emailSender = new EmailSender(_imapClientDirector);
            _emailSender.Setup();
        }

        public void AddNotification(AbstractNotification action)
        {
            NotificationActions.Add(action);
        }

        private bool FreshenMailBox()
        {
            var templist = EmailList;

            EmailList.Clear();

            try
            {
                EmailList.AddRange(_imapWorker.FreshenMailBox().Result);
            }
            catch (Exception ex)
            {
                Exceptions.Add(ex);
                EmailList = templist;
                return false;
            }

            using (var ctx = new MailModelContainer())
            {
                foreach (var email in EmailList.Where(email => _mbLogger.LogEmailReceived(email)))
                {
                    NotificationActions.ForEach(x => x?.Notify(email, NotificationType.Received, MailBoxName));
                    NewMessageReceived?.Invoke(email, EventArgs.Empty);

                    foreach(var em in ctx.Emails.Where(x => !x.InQueue && x.ImapMailBoxConfigurationId == _config.Id && x.EnvelopeID.Equals(email.Envelope.MessageId)))
                    {
                        em.InQueue = true;
                    }
                }

                ctx.SaveChanges();
            }

            foreach (var summary in templist.Where(summary => !EmailList.Any(x => x.Envelope.MessageId.Equals(summary.Envelope.MessageId))))
            {
                _mbLogger.LogEmailRemoved(summary);
            }


            //take care of any emails that may have been left as marked in queue from previous shutdown/disconnect
            using (var ctx = new MailModelContainer())
            {
                foreach (var email in ctx.Emails.Where(email => email.InQueue && email.ImapMailBoxConfigurationId == _config.Id))
                {
                    if (!templist.Any(x => x.Envelope.MessageId.Equals(email.EnvelopeID)))
                    {
                        email.InQueue = false;
                    }
                }
                ctx.SaveChanges();
            }
            return true;
        }

        private void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            var messages = _imapWorker.GetNewMessages(eventArgs.Count).Result;

            foreach (var message in messages)
            {
                if (message?.Envelope == null || EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))) continue;

                EmailList.Add(message);
                NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Received, MailBoxName));
                NewMessageReceived?.Invoke(message, EventArgs.Empty);
                _mbLogger.LogEmailReceived(message);
            }

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.FreshenMailBox(MailBoxName);
        }

        private void ImapIdlerOnMessageExpunged(object sender, MessageEventArgs messageEventArgs)
        {
            if (messageEventArgs.Index > EmailList.Count || EmailList[messageEventArgs.Index] == null) return;

            var message = EmailList[messageEventArgs.Index];

            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Removed, MailBoxName));
            MessageRemoved?.Invoke(message, EventArgs.Empty);
            _mbLogger.LogEmailRemoved(message);
            
            EmailList.RemoveAt(messageEventArgs.Index);

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.FreshenMailBox(MailBoxName);
        }
        

        private void ImapIdlerOnMessageSeen(object sender, MessageFlagsChangedEventArgs eventArgs)
        {
            var message = _imapWorker.GetMessageSummary(eventArgs.Index).Result;
            _mbLogger.LogEmailSeen(message);
            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Seen, MailBoxName));
        }

        public MimeMessage GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);

            try
            {
                return _imapWorker.GetMessage(uid).Result;
            }
            catch (AggregateException ex)
            {
                logger.Error(ex);
                Exceptions.Add(ex);
                return null;
            }
        }


        //todo this probably doesn't belong here - maybe another class has this responsibility?
        public bool SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

            if (!_emailSender.SendMail(message, uniqueId, emailDestination, moveToDest).Result) return false;

            if (moveToDest) _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);

            return true;
        }

        public void MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy)
        {
            _mbLogger.LogEmailChanged(summary, actionTakenBy, "Moved");
            _imapWorker.MoveMessage(summary.UniqueId.Id, moveToFolder, MailBoxName);
        }

        public void MoveMessage(uint uid, string messageid, string moveToFolder, string actionTakenBy)
        {
            _mbLogger.LogEmailChanged(messageid, actionTakenBy, "Moved to " + moveToFolder);
            _imapWorker.MoveMessage(uid, moveToFolder, MailBoxName);
        }
    }
}