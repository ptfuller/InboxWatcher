using System;
using System.Collections.Generic;
using System.Linq;
using MailKit;

namespace InboxWatcher
{
    public class ImapMailBox
    {
        private readonly ImapIdler _imapIdler;
        private readonly ImapPoller _imapPoller;

        public ImapMailBox()
        {
            _imapPoller = new ImapPoller(ImapClientDirector.GetReadyClient());
            _imapIdler = new ImapIdler(ImapClientDirector.GetReadyClient());
            EmailList = new List<IMessageSummary>();

            _imapIdler.MessageArrived += ImapIdlerOnMessageArrived;
        }

        public IList<IMessageSummary> EmailList { get; set; }

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

            foreach (
                var message in
                    messages.Where(
                        message => !EmailList.Any(x => x.Envelope.MessageId.Equals(message.Envelope.MessageId))))
            {
                EmailList.Add(message);
            }
        }
    }
}