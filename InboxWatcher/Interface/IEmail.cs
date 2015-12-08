using System.Collections.Generic;

namespace InboxWatcher
{
    public interface IEmail
    {
        int Id { get; set; }
        bool InQueue { get; set; }
        int Minutes { get; set; }
        string Sender { get; set; }
        System.DateTime TimeReceived { get; set; }
        string Subject { get; set; }
        bool MarkedAsRead { get; set; }
        string BodyText { get; set; }
        string EnvelopeID { get; set; }
        int ImapMailBoxConfigurationId { get; set; }
        ImapMailBoxConfiguration ImapMailBoxConfiguration { get; set; }
    }
}