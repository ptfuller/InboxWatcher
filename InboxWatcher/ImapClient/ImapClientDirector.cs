﻿using System.Threading.Tasks;
using MailKit.Net.Smtp;

namespace InboxWatcher
{
    public class ImapClientDirector
    {
        private ImapClientBuilder Builder { get; }

        public string SendAs { get; set; }
        public string UserName { get; set; }

        public ImapClientDirector(IClientConfiguration configuration)
        {
            Builder = new ImapClientBuilder()
                .WithHost(configuration.HostName)
                .WithPassword(configuration.Password)
                .WithPort(configuration.Port)
                .WithUseSecure(configuration.UseSecure)
                .WithUserName(configuration.UserName)
                .WithSmtpSendName(configuration.UserName);


            //todo add this value to the configuration object
            UserName = configuration.UserName;
            SendAs = UserName;
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