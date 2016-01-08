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
        protected Timer TaskCheckTimer;
        protected readonly ImapClientDirector Director;
        protected Task IdleTask;
        private int _numTimesIdleBusy;

        protected bool AreEventsSubscribed;

        private EventHandler<MessagesArrivedEventArgs> messageArrived;
        public event EventHandler<MessagesArrivedEventArgs> MessageArrived
        {//make sure only 1 subscription to these event handlers
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

            TaskCheckTimer = new Timer(20000);
            TaskCheckTimer.Elapsed += (s, e) => CheckTask();
            TaskCheckTimer.AutoReset = false;
            TaskCheckTimer.Enabled = true;
        }

        public virtual void Setup()
        {
            _numTimesIdleBusy = 0;
            AreEventsSubscribed = false;

            ImapClient?.Dispose();

            try
            {
                ImapClient = Director.GetReadyClient();
                ImapClient.Disconnected += (sender, args) => { Setup(); };
            }
            catch (Exception ex)
            {
                var exception = new Exception(GetType().Name + " Problem getting imap client - trying again in 10 seconds ", ex);
                HandleException(exception);
                Thread.Sleep(10000);
                Setup();
            }

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

                if (!ImapClient.IsConnected || !ImapClient.IsAuthenticated) Setup();

                if (!ImapClient.Inbox.IsOpen)
                {
                    try
                    {
                        ImapClient.Inbox.Open(FolderAccess.ReadWrite);
                    }
                    catch (InvalidOperationException ex)
                    {
                        var exception = new Exception(GetType().FullName + " Error at Idle Loop", ex);
                        HandleException(exception);
                        Setup();
                    }
                }

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

        protected void CheckTask()
        {
            Debug.WriteLine("Checking Idle Task for " + GetType().Name);

            if (!ImapClient.IsIdle)
            {
                Debug.WriteLine("Imap client is busy");
                return;
            }

            if (!ImapClient.IsIdle && GetType() == typeof (ImapIdler))
            {
                if (_numTimesIdleBusy >= 5)
                {
                    Debug.WriteLine("Client is stuck idle!  Resetting client");
                    Setup();
                    _numTimesIdleBusy = 0;
                }
                _numTimesIdleBusy++;
            }

            if (IdleTask.IsFaulted)
            {
                Debug.WriteLine("Idle task is faulted!  Resetting client.");
                Setup();
            }

            TaskCheckTimer.Start();
        }
    }
}