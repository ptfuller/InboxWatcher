using System;
using System.Threading;
using MailKit;
using Timer = System.Timers.Timer;

namespace InboxWatcher
{
    public class ImapIdler
    {
        private static IImapClient _imapClient;
        private CancellationTokenSource _cancelToken;
        private CancellationTokenSource _doneToken;
        private Timer _timeout;

        public ImapIdler(IImapClient imapClient)
        {
            _imapClient = imapClient;
        }

        public event EventHandler MessageArrived;

        public void StartIdling()
        {
            _doneToken = new CancellationTokenSource();
            _cancelToken = new CancellationTokenSource();

            _imapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;

            _imapClient.IdleAsync(_doneToken.Token, _cancelToken.Token);

            IdleLoop();
        }

        private void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            MessageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }

        private void IdleLoop()
        {
            _timeout = new Timer(9*60*1000);
            _timeout.Elapsed += (s, e) =>
            {
                _doneToken.Cancel();
                StartIdling();
            };
            _timeout.AutoReset = false;
            _timeout.Start();
        }
    }
}