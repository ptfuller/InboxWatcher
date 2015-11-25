using System;

namespace InboxWatcher
{
    public interface ISummary
    {
        string Subject { get; set; } 
        DateTime Received { get; set; }
        string Sender { get; set; }
        string UniqueId { get; set; }
    }
}