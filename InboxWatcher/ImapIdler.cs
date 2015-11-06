using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MailKit;
using MailKit.Net.Imap;

namespace InboxWatcher
{
    public class ImapIdler
    {
        private static IImapClient _imapClient;
        private CancellationTokenSource _doneToken;
        private CancellationTokenSource _cancelToken;

        public event EventHandler MessageArrived;

        public ImapIdler(IImapClient imapClient)
        {
            _imapClient = imapClient;
            _doneToken = new CancellationTokenSource();
            _cancelToken = new CancellationTokenSource();
        }

        public void StartIdling()
        {
            _imapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;
            _imapClient.IdleAsync(_doneToken.Token, _cancelToken.Token);
        }

        private void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            MessageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }
    }
}
