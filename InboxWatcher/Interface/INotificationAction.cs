using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher
{
    public interface INotificationAction
    {
        bool Notify(IMessageSummary summary, NotificationType notificationType);
    }
}