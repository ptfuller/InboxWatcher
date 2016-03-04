using System.Threading.Tasks;
using InboxWatcher.ImapClient;

namespace InboxWatcher.Interface
{
    public interface IImapFactory
    {
        string MailBoxName { get; set; }
        Task<IImapClient> GetClient();
        Task<SendClient> GetSmtpClient();
        ImapMailBox GetMailBox();
        IClientConfiguration GetConfiguration();
    }
}