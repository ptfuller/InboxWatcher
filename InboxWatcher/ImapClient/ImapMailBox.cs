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
            //if setup fails let's try again soon
            if (!SetupClients())
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(_retryTime);
                    _retryTime *= 2;
                    Setup();
                });

                return;
            }

            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
            _imapIdler.MessageExpunged += ImapIdlerOnMessageExpunged;
            _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;

            //exceptions that happen in the idler or worker get logged in a list in the mailbox
            _imapIdler.ExceptionHappened += (sender, args) => Exceptions.Add((Exception) sender);
            _imapWorker.ExceptionHappened += (sender, args) => Exceptions.Add((Exception) sender);

            //imap idler starts idling after the GetMailFolders call below
            _imapWorker.StartIdling();

            //make worker get initial list of messages and then start idling
            FreshenMailBox();

            //get folders
            EmailFolders = _imapIdler.GetMailFolders();
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
            }
            catch (AggregateException ex)
            {
                Exceptions.AddRange(ex.InnerExceptions);
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
            EmailList.Clear();
            EmailList.AddRange(_imapWorker.FreshenMailBox());

            foreach (var email in EmailList.Where(email => _mbLogger.LogEmailReceived(email)))
            {
                NotificationActions.ForEach(x => x?.Notify(email, NotificationType.Received));
                NewMessageReceived?.Invoke(email, EventArgs.Empty);
            }
        }

        private void ImapIdlerOnMessageArrived(object sender, MessagesArrivedEventArgs eventArgs)
        {
            var messages = _imapWorker.GetNewMessages(eventArgs.Count);
            
            foreach (var message in messages)
            {
                EmailList.Add(message);
                NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Received));
                NewMessageReceived?.Invoke(message, EventArgs.Empty);
                _mbLogger.LogEmailReceived(message);
            }
        }

        private void ImapIdlerOnMessageExpunged(object sender, MessageEventArgs messageEventArgs)
        {
            var message = EmailList.FirstOrDefault(x => x.Index == messageEventArgs.Index);

            if (message == null) return;

            NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Removed));
            MessageRemoved?.Invoke(message, EventArgs.Empty);
            _mbLogger.LogEmailRemoved(message);
            
            ReorderEmailList(EmailList.IndexOf(message));
        }
        
        private void ReorderEmailList(int index){
            
            for (int i = index; i < EmailList.Count; i++){
                EmailList[i]--;
            }
            
            EmailList.RemoveAt(index);
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

                if (moveToDest)
                {
                    _imapWorker.MoveMessage(uniqueId, emailDestination, MailBoxName);
                }

                _mbLogger.LogEmailSent(message, emailDestination, moveToDest);

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