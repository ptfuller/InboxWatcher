﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MailKit;
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
                Debug.Write(ex.Message);
                return new List<IMessageSummary>();
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

        public MimeMessage GetMessage(UniqueId uid)
        {
            StopIdle();

            var getToken = new CancellationTokenSource();

            var message = ImapClient.Inbox.GetMessage(uid, getToken.Token);

            StartIdling();

            return message;
        }

    }
}