using InboxWatcher.Interface;
using MailKit;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using NLog;

namespace InboxWatcher.ImapClient
{
    public class ImapClientBuilder
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private string _host;
        private string _password;
        private int _port = 993;
        private int _smtpPort = 587;
        private string _userName;
        private string _sendName;
        private bool _useSecure = true;
        private string _smtpHostName;
        private string _smtpUserName;
        private string _smtpPassword;

        public ImapClientBuilder()
        {
            
        }

        public static IImapClient ToIImapClient(ImapClientBuilder instance)
        {
            return instance.Build().Result;
        }

        public ImapClientBuilder WithHost(string host)
        {
            _host = host;
            return this;
        }

        public ImapClientBuilder WithSmtpSendName(string name)
        {
            _sendName = name;
            return this;
        }

        public ImapClientBuilder WithPort(int port)
        {
            _port = port;
            return this;
        }

        public ImapClientBuilder WithUseSecure(bool useSecure)
        {
            _useSecure = useSecure;
            return this;
        }

        public ImapClientBuilder WithUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        public ImapClientBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public async Task<IImapClient> Build()
        {
            
            var client = new ImapClientWrapper();

            client.ConnectTask = client.ConnectAsync(_host, _port, _useSecure);

            client.AuthenticationMechanisms.Remove("XOAUTH2");

            try
            {
                await client.ConnectTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);   
                throw ex;
            }

            client.AuthTask = client.AuthenticateAsync(_userName, _password);

            try
            {
                await client.AuthTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw ex;
            }
            
            client.InboxOpenTask = client.Inbox.OpenAsync(FolderAccess.ReadWrite);

            try
            {
                await client.InboxOpenTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw ex;
            }

            return client;
        }

        public virtual async Task<SendClient> GetSmtpClient()
        {
            var client = new SendClient();
            client.SendAs = _sendName;

            if (string.IsNullOrEmpty(_sendName))
            {
                _sendName = _userName;
            }

            await client.ConnectAsync(_smtpHostName, _smtpPort);
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(_smtpUserName, _smtpPassword);
            return client;
        }

        public ImapClientBuilder WithSmtpHostName(string smtpHostName)
        {
            _smtpHostName = smtpHostName;
            return this;
        }

        public ImapClientBuilder WithSmtpUserName(string smtpUserName)
        {
            _smtpUserName = smtpUserName;
            return this;
        }

        public ImapClientBuilder WithSmtpPassword(string smtpPassword)
        {
            _smtpPassword = smtpPassword;
            return this;
        }

        public ImapClientBuilder WithSmtpPort(int smtpPort)
        {
            _smtpPort = smtpPort;
            return this;
        }
    }
}