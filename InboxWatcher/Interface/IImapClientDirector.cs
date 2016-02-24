using System.Threading.Tasks;
using InboxWatcher.Interface;

namespace InboxWatcher.ImapClient
{
    public interface IImapClientDirector
    {
        string SendAs { get; set; }
        string UserName { get; set; }
        string MailBoxName { get; set; }
        Task<IImapClient> GetClient();
        Task<SendClient> GetSmtpClient();
    }
}