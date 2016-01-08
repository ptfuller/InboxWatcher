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

namespace InboxWatcher.ImapClient
{
    public class ImapWorker : ImapIdler
    {
        private CancellationTokenSource _fetchCancellationToken;

        public ImapWorker(ImapClientDirector director) : base(director)
        {
        }

        public override void StartIdling()
        {
            DoneToken = new CancellationTokenSource();
            CancelToken = new CancellationTokenSource();

            if (!ImapClient.Inbox.IsOpen) ImapClient.Inbox.Open(FolderAccess.ReadWrite);

            IdleTask = ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);

            IdleLoop();
        }

        public async Task<IMessageSummary> GetMessageSummary(UniqueId uid)
        {
            StopIdle();

            var result = await ImapClient.Inbox.FetchAsync(new List<UniqueId> {uid}, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);
            
            StartIdling();

            return result.First();
        }

        public async Task<IMessageSummary> GetMessageSummary(int index)
        {
            StopIdle();

            var result = await ImapClient.Inbox.FetchAsync(index, index, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            StartIdling();

            return result.First();
        }

        public async Task<MimeMessage> GetMessage(UniqueId uid)
        {
            StopIdle();

            var getToken = new CancellationTokenSource();

            var message = await ImapClient.Inbox.GetMessageAsync(uid, getToken.Token);

            StartIdling();

            return message;
        }

        public async Task<MimeMessage> GetMessage(HeaderSearchQuery query)
        {
            StopIdle();
            
            var uids = await ImapClient.Inbox.SearchAsync(query);

            var results = await ImapClient.Inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            var message = await ImapClient.Inbox.GetMessageAsync(results.First().Index);

            StartIdling();

            return message;
        }

        public async Task<bool> DeleteMessage(UniqueId uid)
        {
            StopIdle();

            try
            {
                    if (ImapClient.Capabilities.HasFlag(ImapCapabilities.UidPlus))
                    {
                        await ImapClient.Inbox.ExpungeAsync(new[] {uid});
                    }
                    else
                    {
                        var delToken = new CancellationTokenSource();
                        await ImapClient.Inbox.AddFlagsAsync(new[] {uid}, MessageFlags.Deleted, null, true, delToken.Token);
                        await ImapClient.Inbox.ExpungeAsync(delToken.Token);
                    }
            }
            catch (Exception ex)
            {
                HandleException(ex);
                Setup();
                return false;
            }

            StartIdling();
            return true;
        }

        public async void MoveMessage(uint uniqueId, string emailDestination, string mbname)
        {
            StopIdle();

            var root = await ImapClient.GetFolderAsync(ImapClient.PersonalNamespaces[0].Path, CancellationToken.None);

                IMailFolder mbfolder;
                IMailFolder destFolder;

                try
                {
                    mbfolder = await root.GetSubfolderAsync(mbname);
                }
                catch (FolderNotFoundException ice)
                {
                    mbfolder = await root.CreateAsync(mbname, false);
                }

                try
                {
                    destFolder = await mbfolder.GetSubfolderAsync(emailDestination);
                }
                catch (FolderNotFoundException ice)
                {
                    destFolder = await mbfolder.CreateAsync(emailDestination, true);
                }

                try
                {
                    await ImapClient.Inbox.MoveToAsync(new UniqueId(uniqueId), destFolder);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    StartIdling();
                    return;
                }

            StartIdling();
        }

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public async Task<IEnumerable<IMessageSummary>> FreshenMailBox()
        {
            StopIdle();

            var count = ImapClient.Inbox.Count - 1;
            var min = 0;

            if (count > 500)
            {
                min = count - 500;
            }

            var result = new List<IMessageSummary>();

            try
            {
                result.AddRange(await ImapClient.Inbox.FetchAsync(min, count, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId));
            }
            catch (Exception ex)
            {
                HandleException(ex);
                Setup();
                return result;
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
            StopIdle();

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
                var getMessageException = new Exception($"GetNewMessages Exception: numNewMessages: {numNewMessages}, total: {ImapClient.Inbox.Count} min: {min} max:{max}", ex);

                HandleException(getMessageException);
                Setup();
                StartIdling();
                return result;
            }

            StartIdling();

            return result;
        }
    }
}