using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using InboxWatcher.ImapClient;
using MailKit;

namespace InboxWatcher.Interface
{
    public interface IImapIdler
    {
        event EventHandler<IntegrityCheckArgs> IntegrityCheck;
        event EventHandler<MessagesArrivedEventArgs> MessageArrived;
        event EventHandler<MessageEventArgsWrapper> MessageExpunged;
        event EventHandler<MessageFlagsChangedEventArgs> MessageSeen;

        int Count();
        Task<IEnumerable<IMailFolder>> GetMailFolders();
        bool IsConnected();
        bool IsIdle();
        Task Setup(bool isRecoverySetup = true);
        Task StartIdling([CallerMemberName] string memberName = "");
    }
}