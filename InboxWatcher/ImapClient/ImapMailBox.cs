using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
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
    public class ImapMailBox : IImapMailBox
    {
        private IImapIdler _imapIdler;
        private IImapWorker _imapWorker;
        private IEmailSender _emailSender;
        private IEmailFilterer _emailFilterer;

        private readonly IMailBoxLogger _mbLogger;
        private readonly IClientConfiguration _config;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentQueue<int> _messagesReceivedQueue = new ConcurrentQueue<int>();
        private readonly List<INotificationAction> _notificationActions = new List<INotificationAction>();
        private readonly List<uint> _currentlyProcessingIds = new List<uint>();

        private bool SetupInProgress { get; set; }
        private bool Freshening { get; set; }

        private Task _newMessagesTask;

        public IEnumerable<IMailFolder> EmailFolders { get; set; } = new List<IMailFolder>();
        public IList<IMessageSummary> EmailList { get; set; } = new List<IMessageSummary>();
        public List<Exception> Exceptions { get; set; } = new List<Exception>();

        public string MailBoxName { get; }
        public int MailBoxId { get; }

        public DateTime WorkerStartTime { get; private set; } 
        public DateTime IdlerStartTime { get; private set; }

        public event EventHandler NewMessageReceived;
        public event EventHandler MessageRemoved;


        public ImapMailBox(IClientConfiguration config, IMailBoxLogger mbLogger, IImapWorker imapWorker, IImapIdler imapIdler, IEmailSender emailSender)
        {
            _config = config;
            _mbLogger = mbLogger;
            _imapWorker = imapWorker;
            _imapIdler = imapIdler;
            _emailSender = emailSender;

            MailBoxName = _config.MailBoxName;
            MailBoxId = _config.Id;
        }


        public virtual async Task Setup()
        {
            SetupInProgress = true;
            
            Trace.WriteLine($"{MailBoxName} starting setup");

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

            Freshening = false;

            //make worker get initial list of messages and then start idling
            while (!await FreshenMailBox())
            {
                Trace.WriteLine($"{MailBoxName} FreshenMailBox failed - retry time is: {retryTime/1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime*2;
                }
            }

            while (!SetupEvents())
            {
                Trace.WriteLine($"{MailBoxName} SetupEvents failed - retry time is: {retryTime / 1000} seconds");
                await Task.Delay(retryTime);

                if (retryTime < 160000)
                {
                    retryTime = retryTime*2;
                }
            }

            Trace.WriteLine($"{MailBoxName} FreshMailBox finished");

            //get folders
            EmailFolders = await _imapWorker.GetMailFolders();

            //setup email filterer
            _emailFilterer = new EmailFilterer(this);

            //filter all new messages
            #pragma warning disable 4014
            Task.Run(async () => { await _emailFilterer.FilterAllMessages(EmailList); }).ConfigureAwait(false);
            #pragma warning restore 4014
            
            WorkerStartTime = DateTime.Now;
            IdlerStartTime = DateTime.Now;

            SetupInProgress = false;
        }

        private bool SetupEvents()
        {
            try
            {
                //setup event handlers
                _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
                _imapIdler.MessageExpunged += ImapIdlerOnMessageExpunged;
                _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;
                _emailSender.ExceptionHappened += EmailSenderOnExceptionHappened;
                _imapIdler.IntegrityCheck += ImapIdlerOnIntegrityCheck;
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
       

        private async Task<bool> SetupClients()
        {
            try
            {
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

        private async void ImapIdlerOnIntegrityCheck(object sender, IntegrityCheckArgs integrityCheckArgs)
        {
            if (integrityCheckArgs.InboxCount == EmailList.Count) return;
            Trace.WriteLine($"{MailBoxName}: Integrity check.  Idler says: {integrityCheckArgs.InboxCount} emails and we have {EmailList.Count} in EmailList");
            await FreshenMailBox();
        }

        private async void EmailSenderOnExceptionHappened(object sender, InboxWatcherArgs inboxWatcherArgs)
        {
            var exception = new Exception(DateTime.Now.ToString(),(Exception) sender);

            Trace.WriteLine($"{MailBoxName}: Exception happened in the email sender: {exception.Message}");
            
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
            await _emailSender.Setup();
        }

        public void AddNotification(INotificationAction action)
        {
            _notificationActions.Add(action);
        }

        private async Task<bool> FreshenMailBox()
        {
            if (Freshening) return false;
            
            Freshening = true;

            Trace.WriteLine($"{MailBoxName}: Freshening");

            var templist = EmailList;

            EmailList.Clear();

            try
            {
                foreach (var email in await _imapWorker.FreshenMailBox())
                {
                    EmailList.Add(email);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                Exceptions.Add(ex);
                EmailList = templist;
                Freshening = false;
                await _imapWorker.Setup().ConfigureAwait(false);
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

                    _notificationActions.ForEach(async x =>
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

                //take care of any emails that may have been left as marked in queue from previous shutdown/disconnect
                foreach (var email in ctx.Emails.Where(email => email.InQueue && email.ImapMailBoxConfigurationId == _config.Id))
                {
                    if (!templist.Any(x => x.Envelope.MessageId.Equals(email.EnvelopeID)))
                    {
                        email.InQueue = false;
                    }
                }

                await ctx.SaveChangesAsync();
            }

            //set any email in db that is marked InQueue but not in current inbox as removed
            foreach (var summary in templist.Where(summary => !EmailList.Any(x => x.Envelope.MessageId.Equals(summary.Envelope.MessageId))))
            {
                await _mbLogger.LogEmailRemoved(summary);
            }
            
            Freshening = false;

            return true;
        }

        private async void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            Trace.WriteLine($"{MailBoxName}: New message event");
            await NewMessageQueue(eventArgs.Count);
        }

        private async Task NewMessageQueue(int count)
        {
            _messagesReceivedQueue.Enqueue(count);

            var currentCount = _messagesReceivedQueue.Count;

            await Task.Delay(2500);

            if (_messagesReceivedQueue.Count > currentCount)
            {
                return;
            }

            await HandleNewMessages().ConfigureAwait(false);
        }


        private async Task HandleNewMessages()
        {
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
            }
            catch (Exception ex)
            {
                Exceptions.Add(ex);
                var exception = new Exception($"{MailBoxName}: Problem during HandleNewMessages", ex);
                Trace.WriteLine(ex.Message);
                logger.Error(exception);
                await _imapWorker.Setup().ConfigureAwait(false);
                return;
            }

            foreach (var message in messages)
            {
                if (message?.Envelope == null || EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))) continue;

                EmailList.Add(message);
                _notificationActions.ForEach(async x =>
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

        private async void ImapIdlerOnMessageExpunged(object sender, MessageEventArgsWrapper messageEventArgs)
        {
            await HandleExpungedMessages(messageEventArgs.Index);
        }

        private async Task HandleExpungedMessages(int index)
        {
            if (index >= EmailList.Count) return;
            if (EmailList[index] == null) return;

            var message = EmailList[index];

            EmailList.RemoveAt(index);

            _notificationActions.ForEach(async x =>
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

            _notificationActions.ForEach(async x =>
            {
                var notify = x?.Notify(message, NotificationType.Seen, MailBoxName);
                if (notify != null) await notify;
            });
        }

        public async Task<MimeMessage> GetMessage(uint uniqueId)
        {
            if (_currentlyProcessingIds.Contains(uniqueId)) return null;

            _currentlyProcessingIds.Add(uniqueId);

            var uid = new UniqueId(uniqueId);

            try
            {
                _currentlyProcessingIds.Remove(uniqueId);
                return await _imapWorker.GetMessage(uid);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Exceptions.Add(ex);
                Trace.WriteLine(ex.Message);
                _currentlyProcessingIds.Remove(uniqueId);
                await _imapWorker.Setup().ConfigureAwait(false);
                return null;
            }
        }


        public async Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            try
            {
                var success = false;

                for (var i = 0; i < 4; i++)
                {
                    if (await _emailSender.SendMail(message, emailDestination, moveToDest))
                    {
                        success = true;
                        break;
                    }
                    await Task.Delay(4000);
                }

                if (!success) return false;
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
                    await _imapWorker.Setup().ConfigureAwait(false);
                    return false;
                }
            }

            await _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

            return true;
        }

        public async Task MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy)
        {
            await MoveMessage(summary.UniqueId.Id, summary.Envelope.MessageId, moveToFolder, actionTakenBy);
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
                await _imapWorker.Setup().ConfigureAwait(false);
                return;
            }

            await _mbLogger.LogEmailChanged(messageid, actionTakenBy, "Moved to " + moveToFolder);
        }

        public async Task<MimeMessage> GetEmailByUniqueId(string messageId)
        {
            //throw new NotImplementedException("Not yet working");
            //var tempWorker = new ImapWorker(_imapClientDirector);
            //await tempWorker.Setup(false);
            //var folders = await tempWorker.GetMailFolders();
            //return await tempWorker.GetEmailByUniqueId(messageId, folders);
            return null;
        }
    }
}