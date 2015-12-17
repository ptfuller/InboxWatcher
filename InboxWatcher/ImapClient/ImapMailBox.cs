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
using MailKit;
using MimeKit;
using WebGrease.Css.Extensions;

namespace InboxWatcher.ImapClient
{
    public class ImapMailBox
    {
        private ImapIdler _imapIdler;
        private ImapWorker _imapWorker;

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
                    Green = _imapIdler.IsConnected() && _imapIdler.IsIdle() && _imapWorker.IsConnected()
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
            EmailList.AddRange(_imapWorker.FreshenMailBox());

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
            var messages = _imapWorker.GetNewMessages(eventArgs.Count);
            
            foreach (var message in messages)
            {
                if (EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))) continue;

                EmailList.Add(message);
                NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Received));
                NewMessageReceived?.Invoke(message, EventArgs.Empty);
                _mbLogger.LogEmailReceived(message);
            }
        }

        private void ImapIdlerOnMessageExpunged(object sender, MessageEventArgs messageEventArgs)
        {
            if (EmailList[messageEventArgs.Index] == null) return;

            var message = EmailList[messageEventArgs.Index];

            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Removed));
            MessageRemoved?.Invoke(message, EventArgs.Empty);
            _mbLogger.LogEmailRemoved(message);
            
            EmailList.RemoveAt(messageEventArgs.Index);
        }
        

        private void ImapIdlerOnMessageSeen(object sender, MessageFlagsChangedEventArgs eventArgs)
        {
            var message = _imapWorker.GetMessageSummary(eventArgs.Index);
            _mbLogger.LogEmailSeen(message);
            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Seen));
        }

        public MimeMessage GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);
            return _imapWorker.GetMessage(uid);
        }


        //todo this probably doesn't belong here - maybe another class has this responsibility?
        public bool SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            try
            {
                var client = ImapClientDirector.GetSmtpClientAsync();

                var buildMessage = new MimeMessage();
                buildMessage.From.Add(new MailboxAddress(ImapClientDirector.SendAs, ImapClientDirector.UserName));
                buildMessage.To.Add(new MailboxAddress(emailDestination, emailDestination));

                buildMessage.ReplyTo.AddRange(message.From.Mailboxes);
                buildMessage.ReplyTo.AddRange(message.Cc.Mailboxes);
                buildMessage.From.AddRange(message.From.Mailboxes);
                buildMessage.From.AddRange(message.Cc.Mailboxes);
                buildMessage.Subject = message.Subject;

                var builder = new BodyBuilder();

                foreach (
                    var bodyPart in message.BodyParts.Where(bodyPart => !bodyPart.ContentType.MediaType.Equals("text")))
                {
                    builder.LinkedResources.Add(bodyPart);
                }

                string addresses = message.From.Mailboxes.Aggregate("",
                        (current, address) => current + (address.Address + " "));

                string ccAddresses = message.Cc.Mailboxes.Aggregate("", (a, b) => a + (b.Address + " "));

                string toAddresses = message.To.Mailboxes.Aggregate("", (a, b) => a + (b.Address + " "));

                if (message.TextBody != null)
                {
                    builder.TextBody = "***Message From " + _config.MailBoxName + "*** \nSent from: " + addresses +
                                       "\nSent to: " + toAddresses +
                                       "\nCC'd on email: " + ccAddresses + "\nMessage Date: " + message.Date.ToLocalTime().ToString("F")
                                       + "\n---\n\n" + message.TextBody;
                }

                if (message.HtmlBody != null)
                {
                    builder.HtmlBody = "<p>***Message From " + _config.MailBoxName + "***<br/><p>Sent from: " + addresses +
                                       "<br/><p>Sent to: " + toAddresses +
                                       "<br/><p>CC'd on email: " + ccAddresses + "<br/><p>Message Date:" + message.Date.ToLocalTime().ToString("F") +
                                       "<br/>---<br/><br/>" + message.HtmlBody;
                }

                buildMessage.Body = builder.ToMessageBody();

                if (!client.ConnectTask.IsCompleted)
                {
                    client.ConnectTask.Wait(5000);
                }

                if (!client.AuthTask.IsCompleted)
                {
                    client.AuthTask.Wait(5000);
                }

                if (!client.IsConnected || !client.IsAuthenticated)
                {
                    client = ImapClientDirector.GetSmtpClient();
                }

                _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

                if (moveToDest)
                {
                    _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);
                }

                client.Send(buildMessage);
            }
            catch (Exception ex)
            {
                //send an email with error message
                return false;
            }

            return true;
        }

    }
}