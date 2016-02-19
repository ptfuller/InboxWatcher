using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using NLog;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class ImapWorker : ImapIdler
    {
        private CancellationTokenSource _fetchCancellationToken;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private Timer _idleTimer;

        public ImapWorker(ImapClientDirector director) : base(director)
        {
            _idleTimer = new Timer(60000);
            _idleTimer.AutoReset = false;
            _idleTimer.Elapsed -= IdleTimerOnElapsed;
            _idleTimer.Elapsed += IdleTimerOnElapsed;
        }

        private async void IdleTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            await StartIdling();
        }

        public async Task<IMessageSummary> GetMessageSummary(UniqueId uid)
        {
            _idleTimer.Stop();
            await StopIdle();

            IList<IMessageSummary> result;

            try
            {
                result = await ImapClient.Inbox.FetchAsync(
                    new List<UniqueId> {uid},
                    MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate,
                    Util.GetCancellationToken());
            }
            catch (Exception ex)
            {
                HandleException(ex, true);
                _idleTimer.Start();
                return null;
            }
            
            _idleTimer.Start();
            return result.First();
        }

        public async Task<IMessageSummary> GetMessageSummary(int index)
        {
            _idleTimer.Stop();
            await StopIdle();

            IList<IMessageSummary> result;

            try
            {
                result =
                    await
                        ImapClient.Inbox.FetchAsync(index, index,
                            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId |
                            MessageSummaryItems.InternalDate, Util.GetCancellationToken());
            }
            catch (Exception ex)
            {
                HandleException(ex, true);
                _idleTimer.Start();
                return null;
            }

            _idleTimer.Start();

            return result.First();
        }

        public async Task<MimeMessage> GetMessage(UniqueId uid)
        {
            _idleTimer.Stop();
            await StopIdle();

            try
            {
                var message = await ImapClient.Inbox.GetMessageAsync(uid, Util.GetCancellationToken());
                _idleTimer.Start();
                return message;
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception Happened at GetMessage", ex);
                HandleException(exception, true);
            }

            _idleTimer.Start();
            return null;
        }

        public async Task<MimeMessage> GetMessage(HeaderSearchQuery query)
        {
            _idleTimer.Stop();
            await StopIdle();

            var uids = await ImapClient.Inbox.SearchAsync(query, Util.GetCancellationToken(1000 * 60 * 5));

            var results =
                await ImapClient.Inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, Util.GetCancellationToken());

            var message = await ImapClient.Inbox.GetMessageAsync(results.First().Index);
            
            _idleTimer.Start();
            return message;
        }

        public async Task<bool> DeleteMessage(UniqueId uid)
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
                        await ImapClient.Inbox.AddFlagsAsync(new[] {uid}, MessageFlags.Deleted, null, true, Util.GetCancellationToken());
                        await ImapClient.Inbox.ExpungeAsync(Util.GetCancellationToken());
                    }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                HandleException(ex);
                _idleTimer.Start();
                return false;
            }

            _idleTimer.Start();
            return true;
        }

        public async Task MoveMessage(uint uniqueId, string emailDestination, string mbname)
        {
            _idleTimer.Stop();
            await StopIdle();
            IMailFolder root;

            try
            {
                root = await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, Util.GetCancellationToken());
            }
            catch (Exception ex)
            {
                HandleException(ex);
                _idleTimer.Start();
                return;
            }

            IMailFolder mbfolder;
                IMailFolder destFolder;

                try
                {
                    mbfolder = await root.GetSubfolderAsync(mbname, Util.GetCancellationToken());
                }
                catch (FolderNotFoundException ice)
                {
                    mbfolder = await root.CreateAsync(mbname, false, Util.GetCancellationToken());
                }

                try
                {
                    destFolder = await mbfolder.GetSubfolderAsync(emailDestination, Util.GetCancellationToken());
                }
                catch (FolderNotFoundException ice)
                {
                    destFolder = await mbfolder.CreateAsync(emailDestination, true, Util.GetCancellationToken());
                }

                try
                {
                    await ImapClient.Inbox.MoveToAsync(new UniqueId(uniqueId), destFolder, Util.GetCancellationToken());
                }
                catch (Exception ex)
                {
                    var exception = new Exception("Exception Thrown during MoveMessage", ex);
                    logger.Error(exception);
                    HandleException(ex, true);
                }

            _idleTimer.Start();
        }

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public async Task<IEnumerable<IMessageSummary>> FreshenMailBox()
        {
            _idleTimer.Stop();
            await StopIdle();
            var result = new List<IMessageSummary>();

            try
            {
                await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken(10000));
                await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken(10000));

                var count = ImapClient.Inbox.Count - 1;

                var min = 0;

                if (count > 500)
                {
                    min = count - 500;
                }

                result.AddRange(
                    await
                        ImapClient.Inbox.FetchAsync(min, count,
                            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, Util.GetCancellationToken()));
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception thrown during FreshenMailBox", ex);
                logger.Error(exception);
                HandleException(exception, true);
            }

            _idleTimer.Start();
            return result;
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

            await ImapClient.Inbox.CloseAsync(false, Util.GetCancellationToken(10000));
            await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken(10000));

            var result = new List<IMessageSummary>();

            try
            {
                var min = ImapClient.Inbox.Count - numNewMessages;

                //array index
                var max = ImapClient.Inbox.Count - 1;

                if (min < 0) min = 0;

                result.AddRange(await ImapClient.Inbox.FetchAsync(min, max, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate,
                    Util.GetCancellationToken()));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                HandleException(ex, true);
            }

            _idleTimer.Start();

            return result;
        }

        internal async Task<MimeMessage> GetEmailByUniqueId(string messageId, IEnumerable<IMailFolder> folders)
        {
            _idleTimer.Stop();
            await StopIdle();

            var query = SearchQuery.HeaderContains("MESSAGE-ID", messageId);

            foreach (var folder in folders)
            {
                await folder.OpenAsync(FolderAccess.ReadOnly);
                Trace.WriteLine($"Looking for the message in:{folder.Name}");
                var result = await folder.SearchAsync(query, Util.GetCancellationToken(1000 * 60 * 5));

                if (result.Count > 0)
                {
                    var msg = folder.GetMessageAsync(result[0], Util.GetCancellationToken());
                    await folder.CloseAsync(false, Util.GetCancellationToken());
                    await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());
                    return await msg;
                }

                await folder.CloseAsync();
            }

            await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken());

            _idleTimer.Start();

            return null;
        }

        public override void Dispose()
        {
            _idleTimer.Dispose();
            base.Dispose();
        }
    }
}