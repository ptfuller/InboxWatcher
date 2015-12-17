using InboxWatcher.Interface;
using MailKit;
using System;
using System.Threading;

namespace InboxWatcher.ImapClient
{
    public class ImapClientBuilder
    {
        private string _host;
        private string _password;
        private int _port = 993;
        private int _smtpPort = 587;
        private string _userName;
        private string _sendName;
        private bool _useSecure = true;
        private bool _smtpUseSSL = false;

        private int WaitTime { get; set; } = 5000;

        public ImapClientBuilder()
        {
            
        }

        public static IImapClient ToIImapClient(ImapClientBuilder instance)
        {
            return instance.Build();
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

        public IImapClient Build()
        {
            var client = new ImapClientWrapper();

            client.ConnectTask = client.ConnectAsync(_host, _port, _useSecure);

            client.AuthenticationMechanisms.Remove("XOAUTH2");

            client.Connected +=
                (sender, args) =>
                    client.AuthTask =
                        client.AuthenticateAsync(_userName, _password);

            client.Authenticated +=
                (sender, args) =>
                    client.InboxOpenTask =
                        client.Inbox.OpenAsync(FolderAccess.ReadWrite);

            return client;
        }

        public virtual SendClient GetSmtpClient()
        {
            var client = new SendClient();
            client.Connect(_host, _smtpPort, _smtpUseSSL);
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            client.Authenticate(_userName, _password);
            return client;
        }

        public IImapClient BuildReady()
        {
            return GetReady(Build());
        }

        public IImapClient GetReady(IImapClient client)
        {
            try {
                if (!client.ConnectTask.IsCompleted)
                {
                    if (!client.ConnectTask.Wait(5000)) return BuildReady();
                }

                if (!client.AuthTask.IsCompleted)
                {
                    if (!client.AuthTask.Wait(5000)) return BuildReady();
                }

                if (!client.InboxOpenTask.IsCompleted)
                {
                    if (!client.InboxOpenTask.Wait(5000)) return BuildReady();
                }

                if (client.IsConnected && client.IsAuthenticated)
                {
                    if (!client.Inbox.IsOpen)
                    {
                        client.Inbox.Open(FolderAccess.ReadWrite);
                    }
                    return client;
                }
            } catch (Exception ex)
            {
                Thread.Sleep(WaitTime);
                WaitTime *= 2;
                return BuildReady();
            }
            return BuildReady();
        }

        public SendClient GetSmtpClientAsync()
        {
            
            var client = new SendClient();
            client.UserName = _userName;
            client.Password = _password;

            if (string.IsNullOrEmpty(_sendName))
            {
                _sendName = _userName;
            }

            client.SendAs = _sendName;

            client.ConnectTask = client.ConnectAsync(_host, _smtpPort, _smtpUseSSL);

            client.AuthenticationMechanisms.Remove("XOAUTH2");

            client.Connected += (sender, args) => client.AuthTask = client.AuthenticateAsync(_userName, _password);

            client.ConnectAsync(_host, _smtpPort, _smtpUseSSL);

            return client;
        }
    }
}