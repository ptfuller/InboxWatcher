using System;
using System.Collections.Generic;
using System.Threading;
using MailKit;

namespace InboxWatcher
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

            var results = ImapClient.Inbox.Fetch(0, -1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
                _fetchCancellationToken.Token);

            StartIdling();

            return results;
        }

    }
}