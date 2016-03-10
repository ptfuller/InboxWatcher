using System.Threading.Tasks;
using InboxWatcher.ImapClient;

namespace InboxWatcher.Interface
{
    public interface IImapFactory
    {
        string MailBoxName { get; }
        Task<IImapClient> GetClient();
        Task<SendClient> GetSmtpClient();
        IClientConfiguration GetConfiguration();
    }
}