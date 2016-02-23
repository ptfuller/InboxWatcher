using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InboxWatcher.DTO;
using InboxWatcher.Notifications;
using MailKit;
using MimeKit;

namespace InboxWatcher.ImapClient
{
    public interface IImapMailBox
    {
        IEnumerable<IMailFolder> EmailFolders { get; set; }
        List<IMessageSummary> EmailList { get; set; }
        string MailBoxName { get; }
        int MailBoxId { get; }
        DateTime WorkerStartTime { get; }
        DateTime IdlerStartTime { get; }
        event EventHandler NewMessageReceived;
        event EventHandler MessageRemoved;
        Task Setup();
        MailBoxStatusDto Status();
        void AddNotification(AbstractNotification action);
        Task<MimeMessage> GetMessage(uint uniqueId);
        Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest);
        Task MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy);
        Task MoveMessage(uint uid, string messageid, string moveToFolder, string actionTakenBy);
        Task<MimeMessage> GetEmailByUniqueId(string messageId);
    }
}