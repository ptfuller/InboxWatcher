using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using InboxWatcher.Interface;
using MailKit;
using NLog;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class ImapIdler : IDisposable
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected IImapClient ImapClient;
        protected CancellationTokenSource CancelToken;
        protected CancellationTokenSource DoneToken;
        protected Timer Timeout;
        protected Timer IntegrityCheckTimer;
        protected readonly ImapClientFactory Factory;
        protected Task IdleTask;
        protected SemaphoreSlim StopIdleSemaphore = new SemaphoreSlim(1);

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


        /// <summary>
        /// Fires every minute with a count of emails in the current inbox.  Use this to verify against count in ImapMailBox
        /// </summary>
        public event EventHandler<IntegrityCheckArgs> IntegrityCheck;

        //************************************************************************************

        public ImapIdler(ImapClientFactory factory)
        {
            Factory = factory;

            Timeout = new Timer(9 * 60 * 1000);
            Timeout.AutoReset = false;
            Timeout.Elapsed += IdleLoop;

            IntegrityCheckTimer = new Timer(60000);
            IntegrityCheckTimer.Elapsed += IntegrityCheckTimerOnElapsed;
        }

        private async void IntegrityCheckTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            //only run if we're fetching all messages
            if (ImapClient.Inbox.Count > 500) return;
            IntegrityCheck?.Invoke(null, new IntegrityCheckArgs(ImapClient.Inbox.Count));
        }

        protected virtual void InboxOnMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs messageFlagsChangedEventArgs)
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
            //todo remove this trace for testing
            Trace.WriteLine($"Idler got a message arrived event current count: {ImapClient.Inbox.Count}");
            messageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }

        public virtual async Task Setup(bool isRecoverySetup = true)
        {
            try
            {
                ImapClient = await Factory.GetClient();
                ImapClient.Disconnected += (sender, args) =>
                {
                    Trace.WriteLine("ImapClient disconnected");
                };

                ImapClient.Inbox.Opened += (sender, args) => { Trace.WriteLine($"{Factory.MailBoxName} {GetType().Name} Inbox opened"); };
                ImapClient.Inbox.Closed += (sender, args) => { Trace.WriteLine($"{Factory.MailBoxName} {GetType().Name} Inbox closed"); };
            }
            catch (Exception ex)
            {
                var exception = new Exception(GetType().Name + " Problem getting imap client", ex);
                logger.Error(exception);
                HandleException(exception, true);

                if (!isRecoverySetup)
                {
                    throw exception;
                }
            }

            IdleTask = null;

            Trace.WriteLine($"{Factory.MailBoxName}: {GetType().Name} setup complete");

            IntegrityCheckTimer.Start();

            await StartIdling();
        }

        public virtual async Task StartIdling([CallerMemberNameAttribute] string memberName = "")
        {
            if (IdleTask != null && !IdleTask.IsCompleted) return;

            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            //only assign these events to the idler
            if (GetType() == typeof(ImapIdler))
            {
                try
                {
                    ImapClient.Inbox.MessagesArrived -= InboxOnMessagesArrived;
                    ImapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;

                    ImapClient.Inbox.MessageExpunged -= Inbox_MessageExpunged;
                    ImapClient.Inbox.MessageExpunged += Inbox_MessageExpunged;

                    ImapClient.Inbox.MessageFlagsChanged -= InboxOnMessageFlagsChanged;
                    ImapClient.Inbox.MessageFlagsChanged += InboxOnMessageFlagsChanged;
                }
                catch (ObjectDisposedException ex)
                {
                    var exception = new Exception(GetType().Name + " Exception Thrown during event subscription", ex);
                    logger.Error(exception);
                    HandleException(exception, true);
                }
            }

            Trace.WriteLine($"{Factory.MailBoxName}: {GetType().Name} starting idle called from {memberName}");

            //idle the imap client and wait for exceptions asynchronously
            IdleTask = Task.Run(async () =>
            {
                try
                {
                    if (!ImapClient.Inbox.IsOpen) await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());
                    await ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);
                }
                catch (Exception ex)
                {
                    var exception = new Exception(GetType().Name + " Exception thrown during idle", ex);
                    HandleException(exception, true);
                }
            });

            //reset idle every 10 minutes
            Timeout.Stop();
            Timeout.Start();
        }

        protected virtual async void IdleLoop(object sender, ElapsedEventArgs args)
        {
            await StopIdle();

            if (!ImapClient.IsConnected || !ImapClient.IsAuthenticated) await Setup();

            if (!ImapClient.Inbox.IsOpen)
            {
                try
                {
                    await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    Trace.WriteLine($"{GetType().FullName}: {ex.Message}");
                    var exception = new Exception(GetType().FullName + " Error at Idle Loop", ex);
                    HandleException(exception, true);
                }
            }

            await StartIdling();
        }

        public virtual bool IsConnected()
        {
            return ImapClient.IsConnected;
        }

        public virtual bool IsIdle()
        {
            return ImapClient.IsIdle;
        }

        public int Count()
        {
            if (ImapClient.IsIdle)
            {
                return ImapClient.Inbox.Count;
            }

            return 0;
        }

        protected virtual async Task StopIdle([CallerMemberNameAttribute] string memberName = "")
        {
            try
            {
                if (!IsIdle()) return;

                await StopIdleSemaphore.WaitAsync(Util.GetCancellationToken(1000));

                if (!IsIdle()) return;

                //Trace.WriteLine($"{Factory.MailBoxName}: {GetType().Name} stopping idle called from {memberName}");

                DoneToken.Cancel();
                await IdleTask;

                await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken());
                await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());

                StopIdleSemaphore.Release();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    StopIdleSemaphore.Release();
                    return;
                }

                StopIdleSemaphore.Release();
                var exception = new Exception(GetType().Name + " Exception thrown during StopIdle()", ex);
                logger.Error(exception);
                HandleException(exception, true);
            }
        }

        public void Destroy()
        {
            ImapClient.Dispose();
        }

        protected void HandleException(Exception ex, bool needReset = false)
        {
            var args = new InboxWatcherArgs {NeedReset = needReset};
            ExceptionHappened?.Invoke(ex, args);
        }

        public async Task<IEnumerable<IMailFolder>> GetMailFolders()
        {
            if (ImapClient.IsIdle) await StopIdle();

            if (ImapClient.Inbox.IsOpen) await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken());

            var allFolders = new List<IMailFolder>();
            
            if (ImapClient.PersonalNamespaces != null)
            {
                var root =
                    await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, Util.GetCancellationToken());
                allFolders.AddRange(GetMoreFolders(root));
            }

            await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());

            await StartIdling();

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

        public virtual void Dispose()
        {
            Timeout.Elapsed -= IdleLoop;
            Timeout.Stop();
            Timeout.Dispose();

            IntegrityCheckTimer.Elapsed -= IntegrityCheckTimerOnElapsed;
            IntegrityCheckTimer.Stop();
            IntegrityCheckTimer.Dispose();

            if (IdleTask.IsCompleted)
            {
                IdleTask.Dispose();
            }
            else
            {
                DoneToken.Cancel();
                CancelToken.Cancel();
            }

            ImapClient.Dispose();
        }

        ~ImapIdler()
        {
            //Trace.WriteLine($"{Factory.MailBoxName}:{GetType().Name}:Disposed");
        }
    }
}