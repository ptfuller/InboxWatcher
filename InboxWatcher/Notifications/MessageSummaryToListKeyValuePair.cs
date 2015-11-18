using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Remoting.Messaging;
using MailKit;

namespace InboxWatcher
{
    public static class MessageSummaryToListKeyValuePair
    {
        public static List<KeyValuePair<string, string>> Convert(IMessageSummary summary)
        {
            var kvp = new List<KeyValuePair<string,string>>();

            kvp.Add(new KeyValuePair<string, string>("EnvelopeID", summary.Envelope.MessageId));
            kvp.Add(new KeyValuePair<string, string>("Sender", summary.Envelope.From.ToString()));
            kvp.Add(new KeyValuePair<string, string>("TimeReceived", summary.Date.ToString()));
            kvp.Add(new KeyValuePair<string, string>("Subject", summary.Envelope.Subject));

            return kvp;
        }
    }
}