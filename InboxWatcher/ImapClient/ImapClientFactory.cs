using System;
using System.Diagnostics;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Ninject.Activation;

namespace InboxWatcher.ImapClient
{
    public class ImapClientFactory : IImapFactory
    {
        public string MailBoxName { get; set; }
        private IClientConfiguration configuration;

        public ImapClientFactory(IClientConfiguration configuration)
        {
            this.configuration = configuration;
            MailBoxName = configuration.MailBoxName;
        }

        public async Task<IImapClient> GetClient()
        {
            var imapClient = new ImapClientWrapper();

            await imapClient.ConnectAsync(configuration.HostName, configuration.Port, configuration.UseSecure,Util.GetCancellationToken(10000));
            
            await imapClient.AuthenticateAsync(configuration.UserName, configuration.Password, Util.GetCancellationToken(10000));

            await imapClient.Inbox.OpenAsync(FolderAccess.ReadWrite, Util.GetCancellationToken(10000));

            return imapClient;
        }

        public ImapMailBox GetMailBox()
        {
            return new ImapMailBox(configuration, this);
        }
    }

    public interface IImapFactory
    {
        string MailBoxName { get; set; }
        Task<IImapClient> GetClient();
        ImapMailBox GetMailBox();
    }
}