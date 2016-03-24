using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InboxWatcher.DTO;
using InboxWatcher.Interface;
using InboxWatcher.Notifications;
using MailKit;
using MimeKit;

namespace InboxWatcher.ImapClient
{
    public interface IImapMailBox
    {
        IEnumerable<IMailFolder> EmailFolders { get; set; }
        IList<IMessageSummary> EmailList { get; set; }
        List<Exception> Exceptions { get; set; }
        string MailBoxName { get; }
        int MailBoxId { get; }
        DateTime WorkerStartTime { get; }
        DateTime IdlerStartTime { get; }
        event EventHandler NewMessageReceived;
        event EventHandler MessageRemoved;
        Task Setup();
        MailBoxStatusDto Status();
        void AddNotification(INotificationAction action);
        Task<MimeMessage> GetMessage(uint uniqueId);
        Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest);
        Task MoveMessage(IMessageSummary summary, string moveToFolder, string actionTakenBy);
        Task MoveMessage(uint uid, string messageid, string moveToFolder, string actionTakenBy);
        Task<MimeMessage> GetEmailByUniqueId(string messageId);
        void Dispose();
        Task MoveMessage(Dictionary<string, List<IMessageSummary>> emailsToMove);
    }
}