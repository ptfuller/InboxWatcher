using System.Threading.Tasks;
using InboxWatcher.Interface;

namespace InboxWatcher.ImapClient
{
    public class ImapClientDirector
    {
        private ImapClientBuilder Builder { get; }

        public string SendAs { get; set; }
        public string UserName { get; set; }
        public string MailBoxName { get; set; }

        public ImapClientDirector(IClientConfiguration configuration)
        {
            Builder = new ImapClientBuilder()
                .WithHost(configuration.HostName)
                .WithPassword(configuration.Password)
                .WithPort(configuration.Port)
                .WithUseSecure(configuration.UseSecure)
                .WithUserName(configuration.UserName)
                .WithSmtpSendName(configuration.MailBoxName)
                .WithSmtpHostName(configuration.SmtpHostName)
                .WithSmtpUserName(configuration.SmtpUserName)
                .WithSmtpPassword(configuration.SmtpPassword)
                .WithSmtpPort(configuration.SmtpPort);


            //todo add this value to the configuration object
            UserName = configuration.UserName;
            SendAs = configuration.MailBoxName;

            MailBoxName = configuration.MailBoxName;
        }

        public virtual async Task<IImapClient> GetClient()
        {
            return await Builder.Build();
        }

        public virtual async Task<SendClient> GetSmtpClient()
        {
            return await Builder.GetSmtpClient();
        }
    }
}