using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using InboxWatcher.ImapClient;
using MailKit;
using MailKit.Search;
using MimeKit;

namespace InboxWatcher.Interface
{
    public interface IImapWorker
    {
        Task<MimeMessage> GetMessage(UniqueId uid);
        Task DeleteMessage(UniqueId uid);
        Task MoveMessage(uint uniqueId, string emailDestination, string mbname);

        /// <summary>
        /// Get the 500 newest message summaries
        /// </summary>
        /// <returns>message summaries for the newest 500 messages in the inbox</returns>
        Task<IEnumerable<IMessageSummary>> FreshenMailBox(string calledFrom = "");

        /// <summary>
        /// Call this with the count from the MessagesArrivedEventArgs of the folder's MessagesArrived event handler
        /// </summary>
        /// <param name="numNewMessages">The number of new messages received</param>
        /// <returns>MessageSummaries of newly received messages</returns>
        Task<IEnumerable<IMessageSummary>> GetNewMessages(int numNewMessages);

        event EventHandler<MessagesArrivedEventArgs> MessageArrived
            //make sure only 1 subscription to these event handlers
            ;

        event EventHandler<MessageEventArgs> MessageExpunged;
        event EventHandler<MessageFlagsChangedEventArgs> MessageSeen;

        /// <summary>
        /// Fires every minute with a count of emails in the current inbox.  Use this to verify against count in ImapMailBox
        /// </summary>
        event EventHandler<IntegrityCheckArgs> IntegrityCheck;

        Task Setup(bool isRecoverySetup = true);
        Task StartIdling([CallerMemberName] string memberName = "");
        bool IsConnected();
        bool IsIdle();
        int Count();
        Task<IEnumerable<IMailFolder>> GetMailFolders();
    }
}