using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MailKit;
using MailKit.Net.Imap;
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

            IdleTask = ImapClient.IdleAsync(DoneToken.Token, CancelToken.Token);

            IdleLoop();
        }

        public IMessageSummary GetMessageSummary(UniqueId uid)
        {
            StopIdle();

            IList<IMessageSummary> result;

            lock (ImapClient.SyncRoot)
            {
                result = ImapClient.Inbox.Fetch(new List<UniqueId> {uid}, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);
            }

            StartIdling();

            return result.First();
        }

        public IMessageSummary GetMessageSummary(int index)
        {
            StopIdle();

            IList<IMessageSummary> result;

            lock (ImapClient.SyncRoot)
            {
                result = ImapClient.Inbox.Fetch(index, index, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);
            }

            StartIdling();

            return result.First();
        }

        public MimeMessage GetMessage(UniqueId uid)
        {
            StopIdle();

            var getToken = new CancellationTokenSource();

            MimeMessage message;

            lock (ImapClient.SyncRoot)
            {
                message = ImapClient.Inbox.GetMessage(uid, getToken.Token);
            }

            StartIdling();

            return message;
        }

        public bool DeleteMessage(UniqueId uid)
        {
            StopIdle();

            try
            {
                lock (ImapClient.SyncRoot)
                {
                    if (ImapClient.Capabilities.HasFlag(ImapCapabilities.UidPlus))
                    {
                        ImapClient.Inbox.Expunge(new[] {uid});
                    }
                    else
                    {
                        var delToken = new CancellationTokenSource();
                        ImapClient.Inbox.AddFlags(new[] {uid}, MessageFlags.Deleted, null, true, delToken.Token);
                        ImapClient.Inbox.Expunge(delToken.Token);
                    }
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

        public void MoveMessage(uint uniqueId, string emailDestination, string mbname)
        {
            StopIdle();

            lock (ImapClient.SyncRoot)
            {

                var root = ImapClient.GetFolder(ImapClient.PersonalNamespaces[0]);

                IMailFolder mbfolder;
                IMailFolder destFolder;

                try
                {
                    mbfolder = root.GetSubfolder(mbname);
                }
                catch (FolderNotFoundException ice)
                {
                    mbfolder = root.Create(mbname, false);
                }

                try
                {
                    destFolder = mbfolder.GetSubfolder(emailDestination);
                }
                catch (FolderNotFoundException ice)
                {
                    destFolder = mbfolder.Create(emailDestination, true);
                }
                try
                {
                    ImapClient.Inbox.MoveTo(new UniqueId(uniqueId), destFolder);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    StartIdling();
                    return;
                }
            }

            StartIdling();
        }

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        public IEnumerable<IMessageSummary> FreshenMailBox()
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
                lock (ImapClient.SyncRoot)
                {
                    result.AddRange(ImapClient.Inbox.Fetch(min, count, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId));
                }
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
        public IEnumerable<IMessageSummary> GetNewMessages(int numNewMessages)
        {
            StopIdle();

            var result = new List<IMessageSummary>();

            var min = ImapClient.Inbox.Count - numNewMessages;

            //array index
            var max = ImapClient.Inbox.Count - 1;

            if (min < 0) min = 0;

            try
            {
                lock (ImapClient.SyncRoot)
                {
                    result.AddRange(ImapClient.Inbox.Fetch(min, max, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId));
                }
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