using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MailKit.Net.Imap;

namespace InboxWatcher
{
    public class ImapIdler
    {
        private ImapClient _imapClient;

        public ImapIdler(ImapClient imapClient)
        {
            _imapClient = imapClient;
        }
    }
}
