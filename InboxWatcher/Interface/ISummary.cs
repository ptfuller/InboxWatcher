using System;
using System.Collections.Generic;

namespace InboxWatcher.Interface
{
    public interface ISummary
    {
        string Subject { get; set; } 
        DateTime Received { get; set; }
        Dictionary<string, string> Sender { get; set; }
        string EnvelopeId { get; set; }
        uint UniqueId { get; set; }
        Dictionary<string, string> CcLine { get; set; }
    }
}