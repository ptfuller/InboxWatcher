using System;
using System.Diagnostics;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using MailKit.Security;
using Ninject;
using Ninject.Activation;
using Ninject.Parameters;

namespace InboxWatcher.ImapClient
{
    public class ImapClientFactory : IImapFactory
    {
        public string MailBoxName { get; }

        private readonly IClientConfiguration _configuration;

        public ImapClientFactory(IClientConfiguration config)
        {
            _configuration = config;
            MailBoxName = config.MailBoxName;
        }

        public async Task<IImapClient> GetClient()
        {
            var imapClient = new ImapClientWrapper();

            await imapClient.ConnectAsync(_configuration.HostName, _configuration.Port, _configuration.UseSecure,Util.GetCancellationToken(10000));
            
            await imapClient.AuthenticateAsync(_configuration.UserName, _configuration.Password, Util.GetCancellationToken(10000));

            await imapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken(10000));

            return imapClient;
        }

        public async Task<SendClient> GetSmtpClient()
        {
            var client = new SendClient();
            client.SendAs = _configuration.MailBoxName;

            await client.ConnectAsync(_configuration.SmtpHostName, _configuration.SmtpPort, SecureSocketOptions.Auto, Util.GetCancellationToken(10000));
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(_configuration.SmtpUserName, _configuration.SmtpPassword, Util.GetCancellationToken(10000));
            return client;
        }

        public IClientConfiguration GetConfiguration()
        {
            return _configuration;
        }
    }
}