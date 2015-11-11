using System;
using System.Threading;
using MailKit;
using Timer = System.Timers.Timer;

namespace InboxWatcher
{
    public class ImapIdler
    {
        protected IImapClient ImapClient;
        protected CancellationTokenSource _cancelToken;
        protected CancellationTokenSource _doneToken;
        protected Timer _timeout;
        protected readonly ImapClientDirector _director;

        public event EventHandler MessageArrived;
        public event EventHandler MessageExpunged;

        public ImapIdler(ImapClientDirector director)
        {
            _director = director;
            ImapClient = _director.GetReadyClient();
            ImapClient.Disconnected += (sender, args) => { ImapClient = _director.GetReadyClient(); };

            StartIdling();
        }

        public virtual void StartIdling()
        {
            _doneToken = new CancellationTokenSource();
            _cancelToken = new CancellationTokenSource();

            ImapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;
            ImapClient.Inbox.MessageExpunged += Inbox_MessageExpunged;

            ImapClient.IdleAsync(_doneToken.Token, _cancelToken.Token);

            IdleLoop();
        }

        protected virtual void Inbox_MessageExpunged(object sender, MessageEventArgs e)
        {
            MessageExpunged?.Invoke(sender, e);
        }

        protected virtual void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            MessageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }

        protected virtual void IdleLoop()
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