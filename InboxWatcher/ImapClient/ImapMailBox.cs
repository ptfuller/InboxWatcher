using System;
using System.Collections.Generic;
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
using WebGrease.Css.Extensions;

namespace InboxWatcher.ImapClient
{
    public class ImapMailBox
    {
        private ImapIdler _imapIdler;
        private ImapWorker _imapWorker;
        private EmailSender _emailSender;

        private readonly MailBoxLogger _mbLogger;
        private readonly IClientConfiguration _config;

        private int _retryTime = 5000;
        private Task _recoveryTask;

        private List<AbstractNotification> NotificationActions = new List<AbstractNotification>();

        public IEnumerable<IMailFolder> EmailFolders { get; set; } = new List<IMailFolder>();
        public List<IMessageSummary> EmailList { get; set; } = new List<IMessageSummary>();
        public List<Exception> Exceptions = new List<Exception>();
        public static ImapClientDirector ImapClientDirector { get; set; }
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
            ImapClientDirector = icd;
            _config = config;
            MailBoxName = _config.MailBoxName;
            _mbLogger = new MailBoxLogger(_config);

            Task.Factory.StartNew(Setup);
        }


        public virtual void Setup()
        {
            if (_recoveryTask != null && !_recoveryTask.IsCompleted && !_recoveryTask.IsFaulted) return;

            //if setup fails let's try again soon
            if (!SetupClients())
            {
                _recoveryTask = Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(_retryTime);
                    _retryTime *= 2;
                    Setup();
                });

                return;
            }
            
            //exceptions that happen in the idler or worker get logged in a list in the mailbox
            _imapIdler.ExceptionHappened -= ImapClientExceptionHappened;
            _imapIdler.ExceptionHappened += ImapClientExceptionHappened;

            _imapWorker.ExceptionHappened -= ImapClientExceptionHappened;
            _imapWorker.ExceptionHappened += ImapClientExceptionHappened;

            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
            _imapIdler.MessageExpunged += ImapIdlerOnMessageExpunged;
            _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;

            //make worker get initial list of messages and then start idling
            FreshenMailBox();

            //get folders
            EmailFolders = _imapIdler.GetMailFolders();

            Exceptions.Clear();
        }

        private void ImapClientExceptionHappened(object sender, EventArgs eventArgs)
        {
            if (!Status().Green)
            {
                var ex = new Exception("Resetting clients", (Exception) sender);
                Exceptions.Add(ex);
                Setup();
                return;
            }

            Exceptions.Add((Exception)sender);

            FreshenMailBox();
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
                
            }
        }

        private bool SetupClients()
        {
            try
            {
                _imapWorker = new ImapWorker(ImapClientDirector);
                WorkerStartTime = DateTime.Now;
                _imapIdler = new ImapIdler(ImapClientDirector);
                IdlerStartTime = DateTime.Now;

                _emailSender = new EmailSender(ImapClientDirector);

                _imapIdler.Setup();
                _imapWorker.Setup();
            }
            catch (AggregateException ex)
            {
                var exception = new Exception("Client setup failed.  Verify client settings and check inner exceptions for more information.", ex);
                Exceptions.Add(exception);
                return false;
            }

            return true;
        }

        public void AddNotification(AbstractNotification action)
        {
            NotificationActions.Add(action);
        }

        private void FreshenMailBox()
        {
            var templist = EmailList;

            EmailList.Clear();
            EmailList.AddRange(_imapWorker.FreshenMailBox().Result);

            foreach (var email in EmailList.Where(email => _mbLogger.LogEmailReceived(email)))
            {
                NotificationActions.ForEach(x => x?.Notify(email, NotificationType.Received));
                NewMessageReceived?.Invoke(email, EventArgs.Empty);
            }

            foreach (var summary in templist.Where(summary => !EmailList.Any(x => x.Envelope.MessageId.Equals(summary.Envelope.MessageId))))
            {
                _mbLogger.LogEmailRemoved(summary);
            }


            //take care of any emails that may have been left as marked in queue from previous shutdown/disconnect
            using (var ctx = new MailModelContainer())
            {
                foreach (var email in ctx.Emails.Where(email => email.InQueue && email.Id == _config.Id))
                {
                    email.InQueue = false;
                }
                ctx.SaveChanges();
            }
            
        }

        private void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            var messages = _imapWorker.GetNewMessages(eventArgs.Count).Result;

            foreach (var message in messages)
            {
                if (message?.Envelope == null || EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))) continue;

                EmailList.Add(message);
                NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Received));
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

            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Removed));
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
            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Seen));
        }

        public MimeMessage GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);

            MimeMessage result = new MimeMessage();

            try
            {
                result = _imapWorker.GetMessage(uid).Result;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Any(x => x is MessageNotFoundException))
                {
                    var messageId = EmailList.FirstOrDefault(x => x.UniqueId.Id == uniqueId)?.Envelope.MessageId;

                    if (string.IsNullOrEmpty(messageId)) throw ex;

                    var query = SearchQuery.HeaderContains("MESSAGE-ID", messageId);

                    result = _imapWorker.GetMessage(query).Result;
                }
            }

            return result;
        }


        //todo this probably doesn't belong here - maybe another class has this responsibility?
        public bool SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

            if (!_emailSender.SendMail(message, uniqueId, emailDestination, moveToDest).Result) return false;

            if (moveToDest) _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);

            return true;
        }

    }
}