using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Timer = System.Timers.Timer;

namespace InboxWatcher
{
    public class ImapIdler
    {
        protected IImapClient ImapClient;
        protected CancellationTokenSource CancelToken;
        protected CancellationTokenSource DoneToken;
        protected Timer Timeout;
        protected readonly ImapClientDirector Director;
        protected Task IdleTask;

        protected bool AreEventsSubscribed = false;

        public event EventHandler MessageArrived;
        public event EventHandler MessageExpunged;

        public ImapIdler(ImapClientDirector director)
        {
            Director = director;
            ImapClient = Director.GetReadyClient();
            ImapClient.Disconnected += (sender, args) => { ImapClient = Director.GetReadyClient(); };
        }

        public virtual void StartIdling()
        {
            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            if (!AreEventsSubscribed)
            {
                ImapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;
                ImapClient.Inbox.MessageExpunged += Inbox_MessageExpunged;
                AreEventsSubscribed = true;
            }

            IdleTask = ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);

            IdleLoop();
        }

        protected virtual void Inbox_MessageExpunged(object sender, MessageEventArgs e)
        {
            MessageExpunged?.Invoke(sender, e);
        }

        protected virtual void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            if (MessageArrived == null) return;

            MessageArrived.Invoke(sender, messagesArrivedEventArgs);
            Debug.WriteLine(this.GetType().Name + " - ImapIdler - InboxOnMessagesArrived - " + MessageArrived.GetInvocationList().Length + " - subscribers");
        }

        protected virtual void IdleLoop()
        {
            Timeout = new Timer(9*60*1000);
            Timeout.Elapsed += (s, e) =>
            {
                StopIdle();
                StartIdling();
            };
            Timeout.AutoReset = false;
            Timeout.Start();
        }

        protected virtual void StopIdle()
        {
            DoneToken.Cancel();
            IdleTask.Wait(5000);
        }
    }
}