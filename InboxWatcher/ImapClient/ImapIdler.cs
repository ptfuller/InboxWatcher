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
        public event EventHandler<MessageFlagsChangedEventArgs> MessageSeen;

        public ImapIdler(ImapClientDirector director)
        {
            Director = director;
            Setup();
        }

        protected virtual void Setup()
        {
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
                ImapClient.Inbox.MessageFlagsChanged += InboxOnMessageFlagsChanged;
                AreEventsSubscribed = true;
            }

            IdleTask = ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);

            IdleLoop();

            //try
            //{
            //    IdleTask.Wait();
            //}
            //catch (AggregateException ae)
            //{
            //    ae.Handle((x) =>
            //    {
            //        Debug.WriteLine(x);
            //        if (x is InvalidOperationException)
            //        {
            //            AreEventsSubscribed = false;
            //            ImapClient.Dispose();
            //            Setup();
            //            StartIdling();
            //            return true;
            //        }
            //        return false;
            //    });
            //}
        }

        private void InboxOnMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs messageFlagsChangedEventArgs)
        {
            if (messageFlagsChangedEventArgs.Flags.HasFlag(MessageFlags.Seen))
            {
                MessageSeen?.Invoke(sender, messageFlagsChangedEventArgs);
            }
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