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

namespace InboxWatcher.ImapClient
{
    public class ImapMailBox
    {
        private ImapIdler _imapIdler;
        private ImapWorker _imapWorker;
        private readonly IClientConfiguration _config;

        protected List<AbstractNotification> NotificationActions = new List<AbstractNotification>();

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
            Setup();
        }

        public virtual void Setup()
        {
            //if setup fails stop here
            if (!SetupClients())
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(5000);
                    Setup();
                });
            }

            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
            _imapIdler.MessageExpunged += ImapIdlerOnMessageArrived;
            _imapIdler.MessageSeen += ImapIdlerOnMessageSeen;
            _imapIdler.ExceptionHappened += (sender, args) => Exceptions.Add((Exception) sender);
            _imapWorker.ExceptionHappened += (sender, args) => Exceptions.Add((Exception) sender);

            //_imapIdler.StartIdling();
            _imapWorker.StartIdling();

            //make worker get initial list of messages and then start idling
            ImapIdlerOnMessageArrived(null, null);

            //get folders
            EmailFolders = _imapIdler.GetMailFolders();
        }

        public MailBoxStatusDto Status()
        {
            var status = new MailBoxStatusDto()
            {
                Exceptions = Exceptions,
                IdlerConnected = _imapIdler.IsConnected(),
                StartTime = IdlerStartTime.ToLocalTime().ToString("f"),
                IdlerIdle = _imapIdler.IsIdle(),
                WorkerConnected = _imapWorker.IsConnected(),
                WorkerIdle = _imapWorker.IsIdle()
            };

            return status;
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
                foreach (var exception in ex.InnerExceptions)
                {
                    Exceptions.Add(exception);

                    Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(5000);
                        SetupClients();
                    });

                    throw exception;
                }
            }

            return true;
        }

        public void AddNotification(AbstractNotification action)
        {
            NotificationActions.Add(action);
        }

        private void ImapIdlerOnMessageArrived(object sender, EventArgs eventArgs)
        {
            var messages = _imapWorker.GetMessageSummaries();

            //there was an error during the fetch
            if (messages == null)
            {
                return;
            }

            if (messages.Count == 0)
            {
                //when last email in queue is removed
                foreach (var messageSummary in EmailList)
                {
                    MessageRemoved?.Invoke(messageSummary, EventArgs.Empty);
                    MailBoxLogger.LogEmailRemoved(messageSummary, _config);
                    NotificationActions.ForEach(x => x?.Notify(messageSummary, NotificationType.Removed));
                }

                EmailList.Clear();

                return;
            }

            //find the messages that were removed from the queue
            for(int i = 0; i < EmailList.Count; i++)
            {
                if (!messages.Any(x => x.Envelope.MessageId.Equals(EmailList[i].Envelope.MessageId)))
                {
                    MessageRemoved?.Invoke(EmailList[i], EventArgs.Empty);
                    MailBoxLogger.LogEmailRemoved(EmailList[i], _config);
                    NotificationActions.ForEach(x => x?.Notify(EmailList[i], NotificationType.Removed));
                    EmailList.RemoveAt(i);
                }
            }

            //find the messages that were added to the queue
            foreach (var message in messages)
            {
                if (!EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId)))
                {
                    EmailList.Add(message);
                    NewMessageReceived?.Invoke(message, EventArgs.Empty);
                    MailBoxLogger.LogEmailReceived(message, _config);
                    NotificationActions.ForEach(x => x?.Notify(message, NotificationType.Received));
                }
            }
        }

        public MimeMessage GetMessage(uint uniqueId)
        {
            var uid = new UniqueId(uniqueId);
            return _imapWorker.GetMessage(uid);
        }


        //todo this probably doesn't belong here - maybe another class has this responsibility?
        public bool SendMail(uint uniqueId, string emailDestination)
        {
            try
            {
                var client = ImapClientDirector.GetSmtpClientAsync();
                var message = GetMessage(uniqueId);

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
                
                client.Send(buildMessage);
            }
            catch (Exception ex)
            {
                //send an email with error message
                return false;
            }

            return true;
        }

        private void ImapIdlerOnMessageSeen(object sender, MessageFlagsChangedEventArgs eventArgs)
        {
            var message = _imapWorker.GetMessageSumamry(eventArgs.Index);
            MailBoxLogger.LogEmailSeen(_config, message);

        }
    }
}