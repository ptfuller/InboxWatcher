using System;
using MailKit;

namespace InboxWatcher
{
    public class Summary : ISummary
    {
        public string Subject { get; set; }
        public DateTime Received { get; set; }
        public string Sender { get; set; }
        public string UniqueId { get; set; }

        public Summary(IMessageSummary msgSummary)
        {
            Subject = msgSummary.Envelope.Subject;
            if (msgSummary.Envelope.Date != null) Received = msgSummary.Envelope.Date.Value.DateTime;
            Sender = msgSummary.Envelope.From[0].Name;
            UniqueId = msgSummary.Envelope.MessageId;
        }

        public Summary()
        {
            
        }
    }
}