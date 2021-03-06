﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using InboxWatcher.Interface;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using NLog;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class ImapWorker : ImapIdler, IImapWorker
    {
        private CancellationTokenSource _fetchCancellationToken;
        private readonly Timer _idleTimer;

        public ImapWorker(IImapFactory factory) : base(factory)
        {
            _idleTimer = new Timer(10000);
            _idleTimer.AutoReset = false;
            _idleTimer.Elapsed -= IdleTimerOnElapsed;
            _idleTimer.Elapsed += IdleTimerOnElapsed;
        }

        private async void IdleTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            await StartIdling();
        }
        

        public async Task<MimeMessage> GetMessage(UniqueId uid)
        {
            _idleTimer.Stop();
            await StopIdle();

            try
            {
                return await ImapClient.Inbox.GetMessageAsync(uid, Util.GetCancellationToken(120000));
            }
            finally
            {
                _idleTimer.Start();
            }
        }

        public async Task DeleteMessage(UniqueId uid)
        {
            _idleTimer.Stop();
            await StopIdle();

            try
            {
                if (ImapClient.Capabilities.HasFlag(ImapCapabilities.UidPlus))
                {
                    await ImapClient.Inbox.ExpungeAsync(new[] {uid}, Util.GetCancellationToken());
                }
                else
                {
                    await
                        ImapClient.Inbox.AddFlagsAsync(new[] {uid}, MessageFlags.Deleted, null, true,
                            Util.GetCancellationToken());
                    await ImapClient.Inbox.ExpungeAsync(Util.GetCancellationToken());
                }
            }
            finally
            {
                _idleTimer.Start();
            }
        }

        public async Task MoveMessage(uint uniqueId, string emailDestination)
        {
            _idleTimer.Stop();
            await StopIdle();
            

                try
                {
                    var destFolder = await GetDestinationFolder(emailDestination);
                    await ImapClient.Inbox.MoveToAsync(new UniqueId(uniqueId), destFolder, Util.GetCancellationToken());
                }
                finally
                {
                    _idleTimer.Start();
                }
        }


        public async Task MoveMessage(List<IMessageSummary> uniqueIds, string emailDestination)
        {
            _idleTimer.Stop();
            await StopIdle();

            try
            {
                var destFolder = await GetDestinationFolder(emailDestination);
                await ImapClient.Inbox.MoveToAsync(uniqueIds.Select(x => x.UniqueId).ToList(), destFolder, Util.GetCancellationToken());
            }
            finally
            {
                _idleTimer.Start();
            }
        }


        private async Task<IMailFolder> GetDestinationFolder(string destinationFolderName)
        {
            var root = await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, Util.GetCancellationToken());

            IMailFolder mbfolder;
            IMailFolder destFolder;

            try
            {
                mbfolder = await root.GetSubfolderAsync(Factory.MailBoxName, Util.GetCancellationToken());
            }
            catch (FolderNotFoundException ice)
            {
                mbfolder = await root.CreateAsync(Factory.MailBoxName, false, Util.GetCancellationToken());
            }

            try
            {
                destFolder = await mbfolder.GetSubfolderAsync(destinationFolderName, Util.GetCancellationToken());
            }
            catch (FolderNotFoundException ice)
            {
                destFolder = await mbfolder.CreateAsync(destinationFolderName, true, Util.GetCancellationToken());
            }

            return destFolder;
        }


        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public async Task<IEnumerable<IMessageSummary>> FreshenMailBox([CallerMemberNameAttribute] string calledFrom = "")
        {
            _idleTimer.Stop();
            await StopIdle();
            var result = new List<IMessageSummary>();

            Trace.WriteLine($"{Factory.MailBoxName}: Worker got call to freshen from {calledFrom}");

            try
            {
                var min = 0;

                if (ImapClient.Inbox.Count > 500)
                {
                    min = ImapClient.Inbox.Count - 500;
                }

                result.AddRange(
                    await
                        ImapClient.Inbox.FetchAsync(min, -1,
                            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId |
                            MessageSummaryItems.InternalDate, Util.GetCancellationToken()));

                return result;
            }
            catch (ImapCommandException ex)
            {
                Trace.WriteLine(ex.Message);

                //you get 1 chance
                if (calledFrom.Equals("FreshenMailBox")) throw ex;

                if (ex.Message.Equals("The IMAP server replied to the 'FETCH' command with a 'NO' response.") ||
                    ex.Message.Equals("The folder is not currently open."))
                {
                    await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken());
                    await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());
                    return await FreshenMailBox();
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                _idleTimer.Start();
            }
        }

        /// <summary>
        /// Call this with the count from the MessagesArrivedEventArgs of the folder's MessagesArrived event handler
        /// </summary>
        /// <param name="numNewMessages">The number of new messages received</param>
        /// <returns>MessageSummaries of newly received messages</returns>
        public async Task<IEnumerable<IMessageSummary>> GetNewMessages(int numNewMessages)
        {
            _idleTimer.Stop();
            await StopIdle();
            
            var result = new List<IMessageSummary>();

            try
            {
                var min = ImapClient.Inbox.Count - numNewMessages;

                if (min < 0) min = 0;

                result.AddRange(await ImapClient.Inbox.FetchAsync(min, -1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate,
                    Util.GetCancellationToken()));

                return result;
            }
            finally
            {
                _idleTimer.Start();
            }
        }
    }
}