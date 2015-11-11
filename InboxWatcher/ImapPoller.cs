using System;
using System.Collections.Generic;
using System.Threading;
using MailKit;

namespace InboxWatcher
{
    public class ImapPoller
    {
        private IImapClient _client;
        private CancellationTokenSource _fetchCancellationToken;
        private ImapIdler _idler;

        public ImapPoller(IImapClient client)
        {
            _client = client;
            _client.Disconnected += ClientOnDisconnected;
        }

        //get a new client
        private void ClientOnDisconnected(object sender, EventArgs eventArgs)
        {
            _client = ImapClientDirector.GetReadyClient();
        }

        public IList<IMessageSummary> GetMessageSummaries()
        {
            _fetchCancellationToken = new CancellationTokenSource();
            return _client.Inbox.Fetch(0, -1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
                _fetchCancellationToken.Token);
        }
    }
}