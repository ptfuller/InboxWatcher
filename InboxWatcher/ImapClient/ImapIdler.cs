using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class ImapIdler
    {
        protected IImapClient ImapClient;
        protected CancellationTokenSource CancelToken;
        protected CancellationTokenSource DoneToken;
        protected Timer Timeout;
        protected readonly ImapClientDirector Director;
        protected Task IdleTask;

        protected bool AreEventsSubscribed;

        private EventHandler<MessagesArrivedEventArgs> messageArrived;
        public event EventHandler<MessagesArrivedEventArgs> MessageArrived
        {
            add
            {
                if (messageArrived == null || !messageArrived.GetInvocationList().Contains(value))
                {
                    messageArrived += value;
                }
            }
            remove { messageArrived -= value; }
        }

        private event EventHandler<MessageEventArgs> messageExpunged;
        public event EventHandler<MessageEventArgs> MessageExpunged
        {
            add
            {
                if (messageExpunged == null || !messageExpunged.GetInvocationList().Contains(value))
                {
                    messageExpunged += value;
                }
            }
            remove { messageExpunged -= value; }
        }

        private event EventHandler<MessageFlagsChangedEventArgs> messageSeen;
        public event EventHandler<MessageFlagsChangedEventArgs> MessageSeen
        {
            add
            {
                if (messageSeen == null || !messageSeen.GetInvocationList().Contains(value))
                {
                    messageSeen += value;
                }
            }
            remove { messageSeen -= value; }
        }

        public event EventHandler ExceptionHappened;

        public ImapIdler(ImapClientDirector director)
        {
            Director = director;
        }

        public virtual void Setup()
        {
            AreEventsSubscribed = false;

            ImapClient?.Dispose();

            ImapClient = Director.GetReadyClient();
            ImapClient.Disconnected += (sender, args) => { Setup(); };

            StartIdling();
        }

        public virtual void StartIdling()
        {
            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            if (!AreEventsSubscribed)
            {
                try
                {
                    ImapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;
                    ImapClient.Inbox.MessageExpunged += Inbox_MessageExpunged;
                    ImapClient.Inbox.MessageFlagsChanged += InboxOnMessageFlagsChanged;
                    AreEventsSubscribed = true;
                }
                catch (ObjectDisposedException ex)
                {
                    HandleException(ex);
                    Thread.Sleep(5000);
                    Setup();
                }
            }

            IdleTask = ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);

            IdleLoop();
        }

        private void InboxOnMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs messageFlagsChangedEventArgs)
        {
            if (messageFlagsChangedEventArgs.Flags.HasFlag(MessageFlags.Seen))
            {
                messageSeen?.Invoke(sender, messageFlagsChangedEventArgs);
            }
        }

        protected virtual void Inbox_MessageExpunged(object sender, MessageEventArgs e)
        {
            messageExpunged?.Invoke(sender, e);
        }

        protected virtual void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            messageArrived?.Invoke(sender, messagesArrivedEventArgs);
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

        public virtual bool IsConnected()
        {
            return ImapClient.IsConnected;
        }

        public virtual bool IsIdle()
        {
            return ImapClient.IsIdle;
        }

        protected virtual void StopIdle()
        {
            try
            {
                DoneToken.Cancel();
                IdleTask.Wait(5000);
            }
            catch (AggregateException ag)
            {
                HandleException(ag);
                Setup();
            }
        }

        public void Destroy()
        {
            ImapClient.Dispose();
        }

        protected void HandleException(Exception ex)
        {
            ExceptionHappened?.Invoke(ex, EventArgs.Empty);
        }

        public IEnumerable<IMailFolder> GetMailFolders()
        {
            if(ImapClient.IsIdle) StopIdle();

            if(ImapClient.Inbox.IsOpen) ImapClient.Inbox.Close();

            var allFolders = new List<IMailFolder>();


            if (ImapClient.PersonalNamespaces != null)
            {
                var root = ImapClient.GetFolder(ImapClient.PersonalNamespaces[0]);
                allFolders.AddRange(GetMoreFolders(root));
            }

            ImapClient.Inbox.Open(FolderAccess.ReadWrite);

            StartIdling();

            return allFolders;
        }

        protected IEnumerable<IMailFolder> GetMoreFolders(IMailFolder folder)
        {
            var results = new List<IMailFolder>();

            foreach (var f in folder.GetSubfolders())
            {
                results.Add(f);

                if (f.Attributes.HasFlag(FolderAttributes.HasChildren))
                {
                    results.AddRange(GetMoreFolders(f));
                }
            }

            return results;
        }
    }
}