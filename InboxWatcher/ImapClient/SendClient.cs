using System.Threading.Tasks;
using MailKit.Net.Smtp;

namespace InboxWatcher
{
    public class SendClient : SmtpClient
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string SendAs { get; set; }
        public Task ConnectTask { get; set; }
        public Task AuthTask { get; set; }
    }
}