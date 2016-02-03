﻿using System;
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
        protected SemaphoreSlim _stopIdleSemaphore = new SemaphoreSlim(1);

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

        public virtual async Task Setup(bool isRecoverySetup = true)
        {
            AreEventsSubscribed = false;

            try
            {
                ImapClient = await Director.GetClient();
                ImapClient.Disconnected += (sender, args) =>
                {
                    Trace.WriteLine("ImapClient disconnected");
                };
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

            Trace.WriteLine($"{Director.MailBoxName}: {GetType().Name} setup complete");

            await StartIdling();
        }

        public virtual async Task StartIdling()
        {
            if (IdleTask != null && !IdleTask.IsCompleted) return;

            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            //this is here because we don't want these events assigned to classes inheriting from ImapIdler (ImapWorker)
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

            //idle the imap client and wait for exceptions asynchronously
            IdleTask = Task.Run(async () =>
            {
                try
                {
                    await ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);
                }
                catch (Exception ex)
                {
                    var exception = new Exception(GetType().Name + " Exception thrown during idle", ex);
                    HandleException(exception, true);
                }
            });

            IdleLoop();
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
            messageArrived?.Invoke(sender, messagesArrivedEventArgs);
        }

        protected virtual void IdleLoop()
        {
            Timeout = new Timer(9*60*1000);
            Timeout.Elapsed += async (s, e) =>
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

        protected virtual async Task StopIdle()
        {
            try
            {
                if (!IsIdle()) return;

                await _stopIdleSemaphore.WaitAsync(Util.GetCancellationToken(1000));

                Trace.WriteLine($"{Director.MailBoxName}: {GetType().Name} stopping idle");

                DoneToken.Cancel();
                await IdleTask;

                _stopIdleSemaphore.Release();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    _stopIdleSemaphore.Release();
                    return;
                }

                _stopIdleSemaphore.Release();
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
    }
}