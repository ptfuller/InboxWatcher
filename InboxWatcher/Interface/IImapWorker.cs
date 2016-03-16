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
        Task Setup(bool isRecoverySetup = true);
        Task StartIdling([CallerMemberName] string memberName = "");
        bool IsConnected();
        bool IsIdle();
        int Count();
        Task<IEnumerable<IMailFolder>> GetMailFolders();
    }
}