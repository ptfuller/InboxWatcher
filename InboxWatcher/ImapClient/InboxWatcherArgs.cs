using System;

namespace InboxWatcher.ImapClient
{
    public class InboxWatcherArgs : EventArgs
    {
         public bool NeedReset { get; set; }
    }
}