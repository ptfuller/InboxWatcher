using System;
using MimeKit;

namespace InboxWatcher.ImapClient
{
    public class InboxWatcherArgs : EventArgs
    {
         public bool NeedReset { get; set; }
        public MimeMessage Message { get; set; }
        public string EmailDestination { get; set; }
        public bool MoveToDest { get; set; }
    }
}