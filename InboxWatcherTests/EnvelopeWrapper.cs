using System;
using MailKit;
using MimeKit;

namespace InboxWatcher.Tests
{
    public interface IEnvelopeWrapper
    {
        string ToString();
        InternetAddressList From { get; }
        InternetAddressList Sender { get; }
        InternetAddressList ReplyTo { get; }
        InternetAddressList To { get; }
        InternetAddressList Cc { get; }
        InternetAddressList Bcc { get; }
        string InReplyTo { get; set; }
        DateTimeOffset? Date { get; set; }
        string MessageId { get; set; }
        string Subject { get; set; }
    }

    public class EnvelopeWrapper : Envelope, IEnvelopeWrapper
    {
         
    }
}