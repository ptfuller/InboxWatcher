using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MailKit;

namespace InboxWatcher
{
    public class ImapMailBox
    {
        private readonly ImapIdler _imapIdler;
        private readonly ImapPoller _imapPoller;
        public List<IMessageSummary> EmailList { get; set; }
        public static ImapClientDirector ImapClientDirector { get; set; }

        public event EventHandler NewMessageReceived;
        public event EventHandler MessageRemoved;

        public ImapMailBox(ImapClientDirector icd)
        {
            ImapClientDirector = icd;
            _imapPoller = new ImapPoller(ImapClientDirector);
            _imapIdler = new ImapIdler(ImapClientDirector);
            EmailList = new List<IMessageSummary>();

            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
            _imapIdler.MessageExpunged += ImapIdlerOnMessageArrived;

            SetupEvent();
        }

        //for testing - need to setup event handler on mocks
        public void SetupEvent()
        {
            _imapIdler.StartIdling();
            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
        }

        private void ImapIdlerOnMessageArrived(object sender, EventArgs eventArgs)
        {
            var messages = _imapPoller.GetMessageSummaries();

            if (messages == null || messages.Count == 0) return;

            //find the messages that were removed from the queue
            for(int i = 0; i < EmailList.Count; i++)
            {
                if (!messages.Contains(EmailList[i]))
                {
                    MessageRemoved?.Invoke(EmailList[i], EventArgs.Empty);
                    EmailList.RemoveAt(i);
                }
            }

            //find the messages that were added to the queue
            foreach (var message in messages)
            {
                if (!EmailList.Contains(message))
                {
                    EmailList.Add(message);
                    NewMessageReceived?.Invoke(message, EventArgs.Empty);
                }
            }
        }
    }
}