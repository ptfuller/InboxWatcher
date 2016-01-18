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
            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            if (!ImapClient.Inbox.IsOpen) await ImapClient.Inbox.OpenAsync(FolderAccess.ReadWrite);

            IdleTask = Task.Run(async () =>
            {
                try
                {
                    await ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token).ConfigureAwait(false);
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
            await StopIdle().ConfigureAwait(false);

            var result = await ImapClient.Inbox.FetchAsync(new List<UniqueId> {uid}, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId).ConfigureAwait(false);
            
            StartIdling();

            return result.First();
        }

        public async Task<IMessageSummary> GetMessageSummary(int index)
        {
            await StopIdle().ConfigureAwait(false);

            var result = await ImapClient.Inbox.FetchAsync(index, index, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId).ConfigureAwait(false);

            StartIdling();

            return result.First();
        }

        public async Task<MimeMessage> GetMessage(UniqueId uid)
        {
            await StopIdle().ConfigureAwait(false);

            var getToken = new CancellationTokenSource();

            try
            {
                var message = await ImapClient.Inbox.GetMessageAsync(uid, getToken.Token).ConfigureAwait(false);
                StartIdling();

                return message;
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception Happened at GetMessage", ex);
                HandleException(exception);
            }

            return null;
        }

        public async Task<MimeMessage> GetMessage(HeaderSearchQuery query)
        {
            await StopIdle().ConfigureAwait(false);
            
            var uids = await ImapClient.Inbox.SearchAsync(query).ConfigureAwait(false);

            var results = await ImapClient.Inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId).ConfigureAwait(false);

            var message = await ImapClient.Inbox.GetMessageAsync(results.First().Index).ConfigureAwait(false);

            StartIdling();

            return message;
        }

        public async Task<bool> DeleteMessage(UniqueId uid)
        {
            await StopIdle().ConfigureAwait(false);

            try
            {
                    if (ImapClient.Capabilities.HasFlag(ImapCapabilities.UidPlus))
                    {
                        await ImapClient.Inbox.ExpungeAsync(new[] {uid}).ConfigureAwait(false);
                }
                    else
                    {
                        var delToken = new CancellationTokenSource();
                        await ImapClient.Inbox.AddFlagsAsync(new[] {uid}, MessageFlags.Deleted, null, true, delToken.Token).ConfigureAwait(false);
                    await ImapClient.Inbox.ExpungeAsync(delToken.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                HandleException(ex);
                return false;
            }

            StartIdling();
            return true;
        }

        public async void MoveMessage(uint uniqueId, string emailDestination, string mbname)
        {
            await StopIdle().ConfigureAwait(false);

            var root = await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, CancellationToken.None).ConfigureAwait(false);

                IMailFolder mbfolder;
                IMailFolder destFolder;

                try
                {
                    mbfolder = await root.GetSubfolderAsync(mbname).ConfigureAwait(false);
            }
                catch (FolderNotFoundException ice)
                {
                    mbfolder = await root.CreateAsync(mbname, false).ConfigureAwait(false);
            }

                try
                {
                    destFolder = await mbfolder.GetSubfolderAsync(emailDestination).ConfigureAwait(false);
            }
                catch (FolderNotFoundException ice)
                {
                    destFolder = await mbfolder.CreateAsync(emailDestination, true).ConfigureAwait(false);
            }

                try
                {
                    await ImapClient.Inbox.MoveToAsync(new UniqueId(uniqueId), destFolder).ConfigureAwait(false);
            }
                catch (Exception ex)
                {
                    var exception = new Exception("Exception Thrown during MoveMessage", ex);
                    logger.Error(exception);
                    throw exception;
                }

            StartIdling();
        }

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public async Task<IEnumerable<IMessageSummary>> FreshenMailBox()
        {
            await StopIdle().ConfigureAwait(false);
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
                            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception thrown during FreshenMailBox", ex);
                logger.Error(exception);
                HandleException(exception);
                //throw exception;
            }

            StartIdling();

            return result;
        }

        /// <summary>
        /// Call this with the count from the MessagesArrivedEventArgs of the folder's MessagesArrived event handler
        /// </summary>
        /// <param name="numNewMessages">The number of new messages received</param>
        /// <returns>MessageSummaries of newly received messages</returns>
        public async Task<IEnumerable<IMessageSummary>> GetNewMessages(int numNewMessages)
        {
            await StopIdle().ConfigureAwait(false);

            var result = new List<IMessageSummary>();

            var min = ImapClient.Inbox.Count - numNewMessages;

            //array index
            var max = ImapClient.Inbox.Count - 1;

            if (min < 0) min = 0;

            try
            {
                result.AddRange(await ImapClient.Inbox.FetchAsync(min, max, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                var getMessageException = new Exception($"GetNewMessages Exception: numNewMessages: {numNewMessages}, total: {ImapClient.Inbox.Count} min: {min} max:{max}", ex);
                HandleException(getMessageException);
                throw getMessageException;
            }

            StartIdling();

            return result;
        }
    }
}