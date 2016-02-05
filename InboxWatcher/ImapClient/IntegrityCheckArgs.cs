using System;

namespace InboxWatcher.ImapClient
{
    public class IntegrityCheckArgs : EventArgs
    {
        public IntegrityCheckArgs(int count)
        {
            InboxCount = count;
        }

         public int InboxCount { get; set; }
    }
}