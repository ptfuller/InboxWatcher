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

        public IList<IMessageSummary> GetMessageSummaries()
        {
            StopIdle();

            _fetchCancellationToken = new CancellationTokenSource();

            var results = new List<IMessageSummary>();

            try
            {
                results.AddRange(ImapClient.Inbox.Fetch(0, -1,
                    MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
                    _fetchCancellationToken.Token));
            }
            catch (Exception ex)
            {
                StopIdle();
                HandleException(ex);
                Setup();
                return results;
            }

            StartIdling();

            return results;
        }

        public IMessageSummary GetMessageSummary(UniqueId uid)
        {
            StopIdle();

            var result = ImapClient.Inbox.Fetch(new List<UniqueId> { uid }, MessageSummaryItems.Envelope);

            StartIdling();

            return result.First();
        }

        public IMessageSummary GetMessageSumamry(int index)
        {
            StopIdle();

            var result = ImapClient.Inbox.Fetch(index, index, MessageSummaryItems.Envelope);

            StartIdling();

            return result.First();
        }

        public MimeMessage GetMessage(UniqueId uid)
        {
            StopIdle();

            var getToken = new CancellationTokenSource();

            var message = ImapClient.Inbox.GetMessage(uid, getToken.Token);
            
            StartIdling();

            return message;
        }

        public bool DeleteMessage(UniqueId uid)
        {
            StopIdle();

            try
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
            catch (Exception ex)
            {
                HandleException(ex);
                StartIdling();
                return false;
            }

            StartIdling();
            return true;
        }
    }
}