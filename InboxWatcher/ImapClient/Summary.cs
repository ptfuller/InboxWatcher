using System;
using System.Collections.Generic;
using System.Linq;
using MailKit;
using MimeKit;
using WebGrease.Css.Extensions;

namespace InboxWatcher
{
    public class Summary : ISummary
    {
        public string Subject { get; set; }
        public DateTime Received { get; set; }
        public Dictionary<string, string> Sender { get; set; }
        public string EnvelopeId { get; set; }
        public uint UniqueId { get; set; }
        public Dictionary<string, string> CcLine { get; set; }

        public Summary(IMessageSummary msgSummary)
        {
            Subject = msgSummary.Envelope.Subject;

            if (msgSummary.Envelope.Date != null) Received = msgSummary.Envelope.Date.Value.DateTime;

            //Mailkit's ID that is unique to the specified folder (typically the inbox)
            UniqueId = msgSummary.UniqueId.Id;

            //email addresses from
            Sender = msgSummary.Envelope.From.InternetAddressListToDictionary();

            //email addresses cc line
            CcLine = msgSummary.Envelope.Cc.InternetAddressListToDictionary();

            //IMAP ID that should be specific to that message
            EnvelopeId = msgSummary.Envelope.MessageId;
        }

        public Summary()
        {
            
        }

    }
}