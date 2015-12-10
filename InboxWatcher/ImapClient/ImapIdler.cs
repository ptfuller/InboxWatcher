using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public event EventHandler MessageArrived;
        public event EventHandler MessageExpunged;
        public event EventHandler<MessageFlagsChangedEventArgs> MessageSeen;
        public event EventHandler ExceptionHappened;

        public ImapIdler(ImapClientDirector director)
        {
            Director = director;
            Setup();
        }

        protected virtual void Setup()
        {
            AreEventsSubscribed = false;

            ImapClient?.Dispose();

            ImapClient = Director.GetReadyClient();
            ImapClient.Disconnected += (sender, args) => { Setup(); };
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
            DoneToken.Cancel();

            try
            {
                IdleTask.Wait(5000);
            }
            catch (AggregateException ag)
            {
                Debug.WriteLine(ag.Message);
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