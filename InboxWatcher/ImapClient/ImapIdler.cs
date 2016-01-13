using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using NLog;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class ImapIdler
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected IImapClient ImapClient;
        protected CancellationTokenSource CancelToken;
        protected CancellationTokenSource DoneToken;
        protected Timer Timeout;
        protected Timer TaskCheckTimer;
        protected readonly ImapClientDirector Director;
        protected Task IdleTask;
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

        public event EventHandler<InboxWatcherArgs> ExceptionHappened;

        public ImapIdler(ImapClientDirector director)
        {
            Director = director;
        }

        public virtual void Setup()
        {
            AreEventsSubscribed = false;

            ImapClient?.Dispose();

            try
            {
                ImapClient = Director.GetReadyClient();
                ImapClient.Disconnected += (sender, args) =>
                {
                    Debug.WriteLine("ImapClient disconnected");
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex);

                var exception = new Exception(GetType().Name + " Problem getting imap client", ex);
                HandleException(exception);
                throw exception;
            }

            StartIdling();
        }

        public virtual void StartIdling()
        {
            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            //this is here because we don't want these events assigned to classes inheriting from ImapIdler
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
                    var exception = new Exception(GetType().Name + " Exception Thrown during event subscription", ex);
                    logger.Error(exception);
                    HandleException(exception, true);
                }
            }

            IdleTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token).Wait();
                }
                catch (Exception ex)
                {
                    var exception = new Exception(GetType().Name + " Exception thrown during idle", ex);
                    HandleException(exception, true);
                }

            });

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
                        logger.Error(ex);

                        var exception = new Exception(GetType().FullName + " Error at Idle Loop", ex);
                        HandleException(exception);
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
            catch (Exception ex)
            {
                var exception = new Exception(GetType().Name + " Exception thrown during StopIdle()", ex);
                logger.Error(exception);
                HandleException(exception);
                throw exception;
            }
        }

        public void Destroy()
        {
            ImapClient.Dispose();
        }

        protected void HandleException(Exception ex, bool needReset = false)
        {
            var args = new InboxWatcherArgs();
            args.NeedReset = needReset;
            ExceptionHappened?.Invoke(ex, args);
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