using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher
{
    public class ImapMailBox
    {
        private readonly ImapIdler _imapIdler;
        private readonly ImapWorker _imapWorker;

        protected INotificationAction NotificationAction;

        public List<IMessageSummary> EmailList { get; set; }
        public static ImapClientDirector ImapClientDirector { get; set; }
        public event EventHandler NewMessageReceived;
        public event EventHandler MessageRemoved;
        public string MailBoxName { get; private set; }

        public ImapMailBox(ImapClientDirector icd, string mailBoxName, INotificationAction notificationAction) :
            this(icd, mailBoxName)
        {
            NotificationAction = notificationAction;
        }

        public ImapMailBox(ImapClientDirector icd, string mailBoxName) : this(icd)
        {
            MailBoxName = mailBoxName;
        }

        public ImapMailBox(ImapClientDirector icd)
        {
            ImapClientDirector = icd;
            _imapWorker = new ImapWorker(ImapClientDirector);
            _imapIdler = new ImapIdler(ImapClientDirector);
            EmailList = new List<IMessageSummary>();

            SetupEvent();
        }

        //for testing - need to setup event handler on mocks
        public void SetupEvent()
        {
            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
            _imapIdler.MessageExpunged += ImapIdlerOnMessageArrived;
            _imapIdler.StartIdling();

            _imapWorker.StartIdling();
        }

        private void ImapIdlerOnMessageArrived(object sender, EventArgs eventArgs)
        {
            var messages = _imapWorker.GetMessageSummaries();

            if (messages == null || messages.Count == 0)
            {
                //when last email in queue is removed
                foreach (var messageSummary in EmailList)
                {
                    MessageRemoved?.Invoke(messageSummary, EventArgs.Empty);
                    NotificationAction?.Notify(messageSummary, NotificationType.Removed);
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
                    NotificationAction?.Notify(EmailList[i], NotificationType.Removed);
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
                    NotificationAction?.Notify(message, NotificationType.Received);
                }
            }
        }
    }
}