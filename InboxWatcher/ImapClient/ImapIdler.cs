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
    public class ImapIdler : IImapIdler
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected IImapClient ImapClient;
        protected CancellationTokenSource CancelToken;
        protected CancellationTokenSource DoneToken;
        protected Timer Timeout;
        protected Timer IntegrityCheckTimer;
        protected readonly IImapFactory Factory;
        protected Task IdleTask;
        protected SemaphoreSlim StopIdleSemaphore = new SemaphoreSlim(1);

        
        public event EventHandler<MessagesArrivedEventArgs> MessageArrived;
        public event EventHandler<MessageEventArgsWrapper> MessageExpunged;
        public event EventHandler<MessageFlagsChangedEventArgs> MessageSeen;
        
        /// <summary>
        /// Fires every minute with a count of emails in the current inbox.  Use this to verify against count in ImapMailBox
        /// </summary>
        public event EventHandler<IntegrityCheckArgs> IntegrityCheck;

        //************************************************************************************

        public ImapIdler(IImapFactory factory)
        {
            Factory = factory;

            Timeout = new Timer(9 * 60 * 1000);
            Timeout.AutoReset = false;
            Timeout.Elapsed += IdleLoop;

            IntegrityCheckTimer = new Timer(120000); //every 2 minutes
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
                MessageSeen?.Invoke(sender, messageFlagsChangedEventArgs);
            }
        }

        protected virtual void Inbox_MessageExpunged(object sender, MessageEventArgs e)
        {
            var wrapperArgs = new MessageEventArgsWrapper(e.Index);
            MessageExpunged?.Invoke(sender, wrapperArgs);
        }

        protected virtual void InboxOnMessagesArrived(object sender, MessagesArrivedEventArgs messagesArrivedEventArgs)
        {
            MessageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }

        public virtual async Task Setup(bool isRecoverySetup = false)
        {
            //we should throw an exception if the initial setup is unable to successfully get a client
            //but after that we know that credentials are good and should handle exceptions for any new clients that we need to create
            if (isRecoverySetup)
            {
                try
                {
                    if (ImapClient != null && ImapClient.IsConnected)
                    {
                        await ImapClient.DisconnectAsync(true, Util.GetCancellationToken(15000));
                    }

                    ImapClient = await Factory.GetClient();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);

                    //wait 10 seconds before trying again
                    await Task.Delay(10000);
                    await Setup(true);
                    return;
                }
            }
            else
            {
                ImapClient = await Factory.GetClient();
            }

            ImapClient.Disconnected += (sender, args) =>
            {
                Trace.WriteLine("ImapClient disconnected");
            };
                
            ImapClient.Inbox.Opened += (sender, args) => { Trace.WriteLine($"{Factory.MailBoxName} {GetType().Name} Inbox opened"); };
            ImapClient.Inbox.Closed += (sender, args) => { Trace.WriteLine($"{Factory.MailBoxName} {GetType().Name} Inbox closed"); };
            
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
                ImapClient.Inbox.MessagesArrived -= InboxOnMessagesArrived;
                ImapClient.Inbox.MessagesArrived += InboxOnMessagesArrived;

                ImapClient.Inbox.MessageExpunged -= Inbox_MessageExpunged;
                ImapClient.Inbox.MessageExpunged += Inbox_MessageExpunged;

                ImapClient.Inbox.MessageFlagsChanged -= InboxOnMessageFlagsChanged;
                ImapClient.Inbox.MessageFlagsChanged += InboxOnMessageFlagsChanged;
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
                    await Setup(true);
                }
            });

            //reset idle every 10 minutes
            Timeout?.Stop();
            Timeout?.Start();
        }

        protected virtual async void IdleLoop(object sender, ElapsedEventArgs args)
        {
            await StopIdle();

            if (!ImapClient.IsConnected || !ImapClient.IsAuthenticated)
            {
                await Setup(true);
                return;
            }

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
                    await Setup(true);
                    return;
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
            return ImapClient.Inbox.Count;
        }

        protected virtual async Task StopIdle([CallerMemberNameAttribute] string memberName = "")
        {
            try
            {
                if (!IsIdle()) return;

                await StopIdleSemaphore.WaitAsync(Util.GetCancellationToken(1000));

                if (!IsIdle()) return;

                Trace.WriteLine($"{Factory.MailBoxName}: {GetType().Name} stopping idle called from {memberName}");

                DoneToken.Cancel();
                await IdleTask;
                await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken());
                await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                await Setup(true);
            }
            finally
            {
                StopIdleSemaphore.Release();
            }
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


        ~ImapIdler()
        {
            Trace.WriteLine($"{Factory.MailBoxName}:{GetType().Name}:Disposed");
        }
    }
}