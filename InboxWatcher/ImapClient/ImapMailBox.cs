using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
using Timer = System.Timers.Timer;

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

        private ConcurrentQueue<int> _messagesReceivedQueue = new ConcurrentQueue<int>();
        private List<AbstractNotification> NotificationActions = new List<AbstractNotification>();

        private bool _setupInProgress { get; set; }
        private bool _freshening { get; set; }
        private int _numNewMessagesProcessing { get; set; }
        private Task MessageProcessingIdler { get; set; }

        private Timer freshenTimer;
        
        public IEnumerable<IMailFolder> EmailFolders { get; set; } = new List<IMailFolder>();
        public List<IMessageSummary> EmailList { get; set; } = new List<IMessageSummary>();
        public List<Exception> Exceptions = new List<Exception>();

        public string MailBoxName { get; private set; }
        public int MailBoxId { get; private set; }

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
            freshenTimer = new Timer(1000 * 60 * 10); //10 minutes
            MailBoxName = _config.MailBoxName;
            MailBoxId = _config.Id;
            _mbLogger = new MailBoxLogger(_config);
        }


        public virtual async Task Setup()
        {
            if (_setupInProgress) return;

            Trace.WriteLine($"{MailBoxName} starting setup");

            _setupInProgress = true;

            int retryTime = 5000;

            //if setup fails let's try again soon
            while (!await SetupClients())
            {
                Trace.WriteLine($"{MailBoxName} SetupClients failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime*2;
                }
            }

            Trace.WriteLine(MailBoxName + " SetupClients finished");

            retryTime = 5000;

            //make worker get initial list of messages and then start idling
            while (!await FreshenMailBox())
            {
                Trace.WriteLine($"{MailBoxName} FreshenMailBox failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime * 2;
                }
            }

            while (!SetupEvents())
            {
                Trace.WriteLine($"{MailBoxName} SetupEvents failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime * 2;
                }
            }

            Trace.WriteLine($"{MailBoxName} FreshMailBox finished");

            //get folders
            EmailFolders = await _imapWorker.GetMailFolders();

            //setup email filterer
            _emailFilterer = new EmailFilterer(this);

            //filter all new messages
            Task.Run(async () => { await _emailFilterer.FilterAllMessages(EmailList); });

            Exceptions.Clear();

            freshenTimer.Elapsed += async (sender, args) =>
            {
                Trace.WriteLine($"{MailBoxName} Freshening due to timer");
                await FreshenMailBox();
            };

            freshenTimer.Start();

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
            Trace.WriteLine(MailBoxName + ": " + ex.Message);

            if (needReset)
            {
                Trace.WriteLine($"{MailBoxName}: Something bad happened - resetting clients");
                _imapWorker = null;
                _imapIdler = null;

                await Setup();
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
                Trace.WriteLine(exception.Message);
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
                _emailSender = null;
                _imapIdler = null;
                _imapWorker = null;

                _imapWorker = new ImapWorker(_imapClientDirector);
                WorkerStartTime = DateTime.Now;

                _imapIdler = new ImapIdler(_imapClientDirector);
                IdlerStartTime = DateTime.Now;

                _emailSender = new EmailSender(_imapClientDirector);

                _emailSender.ExceptionHappened += EmailSenderOnExceptionHappened;

                //exceptions that happen in the idler or worker get logged in a list in the mailbox
                _imapIdler.ExceptionHappened += ImapClientExceptionHappened;
                _imapWorker.ExceptionHappened += ImapClientExceptionHappened;

                var sender = _emailSender.Setup();
                var idler = _imapIdler.Setup(false);
                var worker = _imapWorker.Setup(false);

                await Task.WhenAll(sender, idler, worker);
            }
            catch (Exception ex)
            {
                var exception = new Exception("Client setup failed.  Verify client settings and check inner exceptions for more information.", ex);
                Exceptions.Add(exception);
                Trace.WriteLine(ex.Message);
                return false;
            }

            return true;
        }

        private async void EmailSenderOnExceptionHappened(object sender, InboxWatcherArgs inboxWatcherArgs)
        {
            var exception = new Exception(DateTime.Now.ToString(),(Exception) sender);

            _emailSender = null;

            //I don't want a bunch of crap in my logs
            if (!exception.InnerException.Message.Equals("Exception happened during SMTP client No Op"))
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

            _emailSender.ExceptionHappened += EmailSenderOnExceptionHappened;

            await _emailFilterer.FilterAllMessages(EmailList);
        }

        public void AddNotification(AbstractNotification action)
        {
            NotificationActions.Add(action);
        }

        private async Task<bool> FreshenMailBox()
        {
            freshenTimer.Stop();

            if (_freshening) return false;

            _freshening = true;

            Trace.WriteLine($"{MailBoxName}: Freshening");

            var templist = EmailList;

            EmailList.Clear();

            try
            {
                EmailList.AddRange(await _imapWorker.FreshenMailBox());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                Exceptions.Add(ex);
                EmailList = templist;
                _freshening = false;
                Setup();
                return false;
            }

            using (var ctx = new MailModelContainer())
            {
                //LogEmailReceived returns false if email already exists in db => foreach email that wasn't previously received
                foreach (var email in EmailList)
                {
                    if (!await _mbLogger.LogEmailReceived(email))
                    {
                        await _mbLogger.LogEmailBackInQueue(email);
                        continue;
                    }

                    NotificationActions.ForEach(async x =>
                    {
                        var notify = x?.Notify(email, NotificationType.Received, MailBoxName);
                        if (notify != null)
                            await notify;
                    });
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

            freshenTimer.Start();

            return true;
        }

        private async void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            _numNewMessagesProcessing++;

            if (_numNewMessagesProcessing >= 5)
            {
                logger.Info(
                    $"{MailBoxName}: Number of new messages being processed is {_numNewMessagesProcessing} - not processing additional messages for 2 minutes to prevent overload");
                Trace.WriteLine(
                    $"{MailBoxName}: Number of new messages being processed is {_numNewMessagesProcessing} - not processing additional messages for 2 minutes to prevent overload");
                
                if (MessageProcessingIdler == null || MessageProcessingIdler.IsCompleted)
                {
                    MessageProcessingIdler = Task.Run(async () =>
                    {
                        await Task.Delay(1000 * 60 * 2);
                        await FreshenMailBox();
                        _numNewMessagesProcessing = 0;
                    });
                }

                return;
            }

            await NewMessageQueue(eventArgs.Count);
        }

        private async Task NewMessageQueue(int count)
        {
            _messagesReceivedQueue.Enqueue(count);
            var bufferCount = _messagesReceivedQueue.Count;

            await Task.Delay(2000);

            //new messages received during the Task.Delay
            if (_messagesReceivedQueue.Count > bufferCount)
            {
                //let the newest message received handle the call to HandleNewMessages()
                return;
            }

            await HandleNewMessages();
        }

        private async Task HandleNewMessages()
        {
            if (_setupInProgress || _freshening) return;

            var numMessages = new List<int>();

            int value;

            while (_messagesReceivedQueue.TryDequeue(out value))
            {
                numMessages.Add(value);
            }

            IEnumerable<IMessageSummary> messages;

            try
            {
                messages = await _imapWorker.GetNewMessages(numMessages.Sum());
                _numNewMessagesProcessing -= numMessages.Sum();
            }
            catch (Exception ex)
            {
                Exceptions.Add(ex);
                Trace.WriteLine(ex.Message);
                _numNewMessagesProcessing -= numMessages.Sum();
                await Setup();
                return;
            }

            foreach (var message in messages)
            {
                if (message?.Envelope == null || EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))) continue;

                EmailList.Add(message);
                NotificationActions.ForEach(async x =>
                {
                    var notify = x?.Notify(message, NotificationType.Received, MailBoxName);
                    if (notify != null)
                        await notify;
                });
                NewMessageReceived?.Invoke(message, EventArgs.Empty);
                await _mbLogger.LogEmailReceived(message);
            }

            //fresh ui via signalr
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

            NotificationActions.ForEach(async x =>
            {
                var notify = x?.Notify(message, NotificationType.Removed, MailBoxName);
                if (notify != null)
                    await notify;
            });

            MessageRemoved?.Invoke(message, EventArgs.Empty);
            await _mbLogger.LogEmailRemoved(message);

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
            await _mbLogger.LogEmailSeen(message);
            Trace.WriteLine($"{MailBoxName}: {message.Envelope.Subject} was marked as read");

            NotificationActions.ForEach(async x =>
            {
                var notify = x?.Notify(message, NotificationType.Seen, MailBoxName);
                if (notify != null) await notify;
            });
        }

        public async Task<MimeMessage> GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);

            try
            {
                return await _imapWorker.GetMessage(uid);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Exceptions.Add(ex);
                Trace.WriteLine(ex.Message);
                await Setup();
                return null;
            }
        }


        public async Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            try
            {
                if (!await _emailSender.SendMail(message, emailDestination, moveToDest))
                {
                    await FreshenMailBox();
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Trace.WriteLine(ex.Message);
                return false;
            }

            if (moveToDest)
            {
                try
                {
                    await _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    Exceptions.Add(ex);
                    Trace.WriteLine(ex.Message);
                    await Setup();
                    return false;
                }
            }

            await _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

            return true;
        }

        public async Task MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy)
        {
            try
            {
                await _imapWorker.MoveMessage(summary.UniqueId.Id, moveToFolder, MailBoxName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                logger.Error(ex);
                Exceptions.Add(ex);
                await Setup();
                return;
            }

            await _mbLogger.LogEmailChanged(summary, actionTakenBy, "Moved to " + moveToFolder);
        }

        public async Task MoveMessage(uint uid, string messageid, string moveToFolder, string actionTakenBy)
        {
            
            try
            {
                await _imapWorker.MoveMessage(uid, moveToFolder, MailBoxName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                logger.Error(ex);
                Exceptions.Add(ex);
                Setup();
                return;
            }

            await _mbLogger.LogEmailChanged(messageid, actionTakenBy, "Moved to " + moveToFolder);
        }

        public async Task<MimeMessage> GetEmailByUniqueId(string messageId)
        {
            //throw new NotImplementedException("Not yet working");
            var tempWorker = new ImapWorker(_imapClientDirector);
            await tempWorker.Setup(false);
            var folders = await tempWorker.GetMailFolders();
            return await tempWorker.GetEmailByUniqueId(messageId, folders);
        }
    }
}