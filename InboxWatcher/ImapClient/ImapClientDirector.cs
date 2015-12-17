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
                .WithSmtpSendName(configuration.SmtpUserName)
                .WithSmtpHostName(configuration.SmtpHostName)
                .WithSmtpUserName(configuration.SmtpUserName)
                .WithSmtpPassword(configuration.SmtpPassword)
                .WithSmtpPort(configuration.SmtpPort);


            //todo add this value to the configuration object
            UserName = configuration.UserName;
            SendAs = UserName;

            MailBoxName = configuration.MailBoxName;
        }

        public virtual IImapClient GetClient()
        {
            return Builder.Build();
        }

        public virtual IImapClient GetReadyClient()
        {
            return Builder.BuildReady();
        }

        public virtual IImapClient GetThisClientReady(IImapClient client)
        {
            return Builder.GetReady(client);
        }

        public virtual SendClient GetSmtpClient()
        {
            return Builder.GetSmtpClient();
        }

        public virtual SendClient GetSmtpClientAsync()
        {
            return Builder.GetSmtpClientAsync();
        }
    }
}