using System.Threading.Tasks;
using MailKit;
using MimeKit;

namespace InboxWatcher.Interface
{
    public interface IMailBoxLogger
    {
        Task<bool> LogEmailReceived(IMessageSummary summary);
        Task LogEmailRemoved(IMessageSummary email);
        Task LogEmailChanged(IMessageSummary email, string actionTakenBy, string action);
        Task LogEmailChanged(string messageId, string actionTakenBy, string action);
        Task LogEmailSeen(IMessageSummary message);
        Task LogEmailSent(MimeMessage message, string emailDestination, bool moved);
        Task LogEmailBackInQueue(IMessageSummary email);
    }
}