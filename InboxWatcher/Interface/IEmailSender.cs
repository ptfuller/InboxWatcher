using System;
using System.Threading.Tasks;
using InboxWatcher.ImapClient;
using MimeKit;

namespace InboxWatcher.Interface
{
    public interface IEmailSender
    {
        event EventHandler<InboxWatcherArgs> ExceptionHappened;
        Task Setup();
        bool IsConnected();
        bool IsAuthenticated();
        Task<bool> SendMail(MimeMessage message, string emailDestination, bool moveToDest);
    }
}