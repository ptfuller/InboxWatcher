using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using NLog;

namespace InboxWatcher.ImapClient
{
    public class ImapWorker : ImapIdler
    {
        private CancellationTokenSource _fetchCancellationToken;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public ImapWorker(ImapClientDirector director) : base(director)
        {
        }

        public override async Task StartIdling()
        {
            if (IdleTask != null && !IdleTask.IsCompleted) return;

            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            if (!ImapClient.Inbox.IsOpen) await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite);

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

        public async Task<IMessageSummary> GetMessageSummary(UniqueId uid)
        {
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
                HandleException(ex);
                StartIdling();
                throw ex;
            }

            await StartIdling();

            return result.First();
        }

        public async Task<IMessageSummary> GetMessageSummary(int index)
        {
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
                HandleException(ex);
                StartIdling();
                throw ex;
            }

            await StartIdling();

            return result.First();
        }

        public async Task<MimeMessage> GetMessage(UniqueId uid)
        {
            await StopIdle();

            try
            {
                var message = await ImapClient.Inbox.GetMessageAsync(uid, Util.GetCancellationToken());
                await StartIdling();

                return message;
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception Happened at GetMessage", ex);
                HandleException(exception, true);
            }

            return null;
        }

        public async Task<MimeMessage> GetMessage(HeaderSearchQuery query)
        {
            await StopIdle();

            var uids = await ImapClient.Inbox.SearchAsync(query, Util.GetCancellationToken(1000 * 60 * 5));

            var results =
                await ImapClient.Inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, Util.GetCancellationToken());

            var message = await ImapClient.Inbox.GetMessageAsync(results.First().Index);

            await StartIdling();

            return message;
        }

        public async Task<bool> DeleteMessage(UniqueId uid)
        {
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
                return false;
            }

            await StartIdling();
            return true;
        }

        public async Task MoveMessage(uint uniqueId, string emailDestination, string mbname)
        {
            await StopIdle();
            IMailFolder root;

            try
            {
                root = await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, Util.GetCancellationToken());
            }
            catch (Exception ex)
            {
                HandleException(ex);
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
                    throw exception;
                }

            await StartIdling();
        }

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public async Task<IEnumerable<IMessageSummary>> FreshenMailBox()
        {
            await StopIdle();
            var result = new List<IMessageSummary>();

            try
            {
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
                HandleException(exception);
                throw exception;
            }

            await StartIdling();

            return result;
        }

        /// <summary>
        /// Call this with the count from the MessagesArrivedEventArgs of the folder's MessagesArrived event handler
        /// </summary>
        /// <param name="numNewMessages">The number of new messages received</param>
        /// <returns>MessageSummaries of newly received messages</returns>
        public async Task<IEnumerable<IMessageSummary>> GetNewMessages(int numNewMessages)
        {
            await StopIdle();

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
                HandleException(ex);
                throw ex;
            }

            await StartIdling();

            return result;
        }

        internal async Task<MimeMessage> GetEmailByUniqueId(string messageId, IEnumerable<IMailFolder> folders)
        {
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
            return null;
        }
    }
}