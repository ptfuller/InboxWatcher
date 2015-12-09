using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher.Interface
{
    public interface INotificationAction
    {
        bool Notify(IMessageSummary summary, NotificationType notificationType);
        string Serialize();
        INotificationAction DeSerialize(string xmlString);
        string GetConfigurationScript();
    }
}