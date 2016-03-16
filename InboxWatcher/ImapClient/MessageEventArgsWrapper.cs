using System;

namespace InboxWatcher.ImapClient
{
    public class MessageEventArgsWrapper : EventArgs
    {
        public int Index { get; private set; }

        internal MessageEventArgsWrapper(int index)
        {
            this.Index = index;
        }
        
    }
}