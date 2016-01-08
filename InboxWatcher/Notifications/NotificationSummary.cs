using System;
using System.Collections.Generic;
using InboxWatcher.Enum;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using MailKit;

namespace InboxWatcher.Notifications
{
    public class NotificationSummary : ISummary
    {
        public string MailBoxName { get; set; }
        public string Subject { get; set; }
        public DateTime Received { get; set; }
        public Dictionary<string, string> Sender { get; set; }
        public string EnvelopeId { get; set; }
        public uint UniqueId { get; set; }
        public Dictionary<string, string> CcLine { get; set; }
        public string NotificationType { get; set; }

        public NotificationSummary()
        {
            
        }

        public NotificationSummary(ISummary sum, NotificationType notificationType)
        {
            Subject = sum.Subject;
            Received = sum.Received;
            Sender = sum.Sender;
            EnvelopeId = sum.EnvelopeId;
            UniqueId = sum.UniqueId;
            CcLine = sum.CcLine;
            NotificationType = notificationType.ToString();
        }

        public NotificationSummary(IMessageSummary sum, NotificationType notificationType) : this(new Summary(sum), notificationType)
        {
           
        }
    }
}