using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.Interface;

namespace InboxWatcher.DTO
{
    public class EmailDto : IEmail
    {
        public int Id { get; set; }
        public bool InQueue { get; set; }
        public int Minutes { get; set; }
        public string Sender { get; set; }
        public DateTime TimeReceived { get; set; }
        public DateTime? TimeSent { get; set; }
        public string Subject { get; set; }
        public bool MarkedAsRead { get; set; }
        public string BodyText { get; set; }
        public string EnvelopeID { get; set; }
        public int ImapMailBoxConfigurationId { get; set; }
        public ICollection<IEmailLog> EmailLogs { get; set; }
        public ImapMailBoxConfiguration ImapMailBoxConfiguration { get; set; }

        public EmailDto() { }

        public EmailDto(Email email)
        {
            Id = email.Id;
            InQueue = email.InQueue;
            Minutes = email.Minutes;
            Sender = email.Sender;
            TimeReceived = email.TimeReceived;
            TimeSent = email.TimeSent;
            Subject = email.Subject;
            MarkedAsRead = email.MarkedAsRead;
            BodyText = email.BodyText;
            EnvelopeID = email.EnvelopeID;
            ImapMailBoxConfigurationId = email.ImapMailBoxConfigurationId;

            EmailLogs = new List<IEmailLog>();

            foreach (var emailLog in email.EmailLogs)
            {
                EmailLogs.Add(new EmailLogDto(emailLog));
            }
        }
    }
}
