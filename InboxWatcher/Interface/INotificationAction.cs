using System.Threading.Tasks;
using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher.Interface
{
    public interface INotificationAction
    {
        Task<bool> Notify(IMessageSummary summary, NotificationType notificationType, string mailBoxName);
        string Serialize();
        INotificationAction DeSerialize(string xmlString);
        string GetConfigurationScript();
        void TestNotification();
    }
}