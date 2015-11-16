using System;
using System.Diagnostics;
using System.Net.Sockets;
using MailKit;

namespace InboxWatcher
{
    public class ImapClientBuilder
    {
        private string _host;
        private string _password;
        private int _port = 993;
        private string _userName;
        private bool _useSecure = true;

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
            var client = new ImapClientAdapter();

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

        public IImapClient BuildReady()
        {
            return GetReady(Build());
        }

        public IImapClient GetReady(IImapClient client)
        {
            
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
                
            return BuildReady();
        }
    }
}