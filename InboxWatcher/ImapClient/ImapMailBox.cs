using System;
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
using Microsoft.Ajax.Utilities;
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
        private bool _freshening { get; set; }

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
            while (!await SetupClients())
            {
                Debug.WriteLine($"{MailBoxName} SetupClients failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime*2;
                }
            }

            Debug.WriteLine(MailBoxName + " SetupClients finished");

            retryTime = 5000;

            //make worker get initial list of messages and then start idling
            while (!await FreshenMailBox())
            {
                Debug.WriteLine($"{MailBoxName} FreshenMailBox failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime * 2;
                }
            }

            while (!SetupEvents())
            {
                Debug.WriteLine($"{MailBoxName} SetupEvents failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime * 2;
                }
            }

            Debug.WriteLine($"{MailBoxName} FreshMailBox finished");

            //get folders
            EmailFolders = await _imapIdler.GetMailFolders().ConfigureAwait(false); ;

            //setup email filterer
            _emailFilterer = new EmailFilterer(this);

            //filter all new messages
            _emailFilterer.FilterAllMessages(EmailList);

            Exceptions.Clear();
            _setupInProgress = false;
        }

        private async void ImapClientExceptionHappened(object sender, InboxWatcherArgs args)
        {
            await HandleExceptions((Exception) sender, args.NeedReset);
        }

        private async Task HandleExceptions(Exception ex, bool needReset)
        {
            if (Exceptions.Count > 100)
            {
                Environment.Exit(1);
            }

            var exception = new Exception(DateTime.Now.ToString(), ex);

            Exceptions.Add(exception);

            if (needReset)
            {
                _imapWorker = null;
                _imapIdler = null;

                await Setup().ConfigureAwait(false);
            }
        }

        private bool SetupEvents()
        {
            try
            {
                //setup event handlers
                _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
                _imapIdler.MessageExpunged += ImapIdlerOnMessageExpunged;
                _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;
            }
            catch (Exception ex)
            {
                var exception = new Exception($"{MailBoxName} Exception happened during SetupEvents", ex);
                logger.Error(exception);
                return false;
            }

            return true;
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

        private async Task<bool> SetupClients()
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

                await _emailSender.Setup();
                await _imapIdler.Setup();
                await _imapWorker.Setup();
            }
            catch (Exception ex)
            {
                var exception = new Exception("Client setup failed.  Verify client settings and check inner exceptions for more information.", ex);
                Exceptions.Add(exception);
                return false;
            }

            return true;
        }

        private async void EmailSenderOnExceptionHappened(object sender, InboxWatcherArgs inboxWatcherArgs)
        {
            var exception = new Exception(DateTime.Now.ToString(),(Exception) sender);

            //I don't want a bunch of crap in my logs
            if (!exception.Message.Equals("Exception happened during SMTP client No Op"))
            {
                Exceptions.Add(exception);
                logger.Info("Exception happened with Email Sender - Creating a new email sender");
            }

            await SetupEmailSender();
        }

        private async Task SetupEmailSender()
        {
            _emailSender = new EmailSender(_imapClientDirector);
            await _emailSender.Setup();

            _emailFilterer.FilterAllMessages(EmailList);
        }

        public void AddNotification(AbstractNotification action)
        {
            NotificationActions.Add(action);
        }

        private async Task<bool> FreshenMailBox()
        {
            if (_freshening) return false;

            _freshening = true;

            var templist = EmailList;

            EmailList.Clear();

            try
            {
                EmailList.AddRange(await _imapWorker.FreshenMailBox().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Exceptions.Add(ex);
                EmailList = templist;
                _freshening = false;
                return false;
            }

            using (var ctx = new MailModelContainer())
            {
                //LogEmailReceived returns false if email already exists in db => foreach email that wasn't previously received
                foreach (var email in EmailList)
                {
                    if (!await _mbLogger.LogEmailReceived(email)) continue;

                    NotificationActions.ForEach(x => x?.Notify(email, NotificationType.Received, MailBoxName));
                    NewMessageReceived?.Invoke(email, EventArgs.Empty);

                    //set any email that is in the inbox to InQueue = true.
                    foreach(var em in ctx.Emails.Where(x => !x.InQueue && x.ImapMailBoxConfigurationId == _config.Id && x.EnvelopeID.Equals(email.Envelope.MessageId)))
                    {
                        em.InQueue = true;
                    }
                }

                ctx.SaveChanges();
            }

            //set any email in db that is marked InQueue but not in current inbox as removed
            foreach (var summary in templist.Where(summary => !EmailList.Any(x => x.Envelope.MessageId.Equals(summary.Envelope.MessageId))))
            {
                await _mbLogger.LogEmailRemoved(summary);
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

            _freshening = false;

            return true;
        }

        private async void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            await HandleNewMessages(eventArgs.Count);
        }

        private async Task HandleNewMessages(int count)
        {
            var messages = await _imapWorker.GetNewMessages(count).ConfigureAwait(false);

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

        private async void ImapIdlerOnMessageExpunged(object sender, MessageEventArgs messageEventArgs)
        {
            await HandleExpungedMessages(messageEventArgs.Index);
        }

        private async Task HandleExpungedMessages(int index)
        {
            if (index >= EmailList.Count) return;
            if (EmailList[index] == null) return;

            var message = EmailList[index];

            EmailList.RemoveAt(index);

            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Removed, MailBoxName));
            MessageRemoved?.Invoke(message, EventArgs.Empty);
            _mbLogger.LogEmailRemoved(message);

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.FreshenMailBox(MailBoxName);
        }
        

        private async void ImapIdlerOnMessageSeen(object sender, MessageFlagsChangedEventArgs eventArgs)
        {
            await HandleMessageSeen(eventArgs.Index);
        }

        private async Task HandleMessageSeen(int index)
        {
            if (index >= EmailList.Count) return;
            if (EmailList[index] == null) return;

            var message = EmailList[index];
            _mbLogger.LogEmailSeen(message);
            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Seen, MailBoxName));
        }

        public async Task<MimeMessage> GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);

            try
            {
                return await _imapWorker.GetMessage(uid).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Exceptions.Add(ex);
                return null;
            }
        }


        //todo this probably doesn't belong here - maybe another class has this responsibility?
        public async Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            try
            {
                if (!await _emailSender.SendMail(message, uniqueId, emailDestination, moveToDest).ConfigureAwait(false))
                {
                    FreshenMailBox();
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);

                _emailSender = new EmailSender(_imapClientDirector);
                _emailSender.Setup();

                return false;
            }

            _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

            if (moveToDest) _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);

            return true;
        }

        public async Task MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy)
        {
            await _mbLogger.LogEmailChanged(summary, actionTakenBy, "Moved to " + moveToFolder);
            _imapWorker.MoveMessage(summary.UniqueId.Id, moveToFolder, MailBoxName);
        }

        public async Task MoveMessage(uint uid, string messageid, string moveToFolder, string actionTakenBy)
        {
            await _mbLogger.LogEmailChanged(messageid, actionTakenBy, "Moved to " + moveToFolder);
            _imapWorker.MoveMessage(uid, moveToFolder, MailBoxName);
        }
    }
}