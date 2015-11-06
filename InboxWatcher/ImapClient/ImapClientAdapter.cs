using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace InboxWatcher
{
    public class ImapClientAdapter : IImapClient
    {
        private ImapClient _imapClient;

        public ImapClientAdapter()
        {
            this._imapClient = new ImapClient();
        }

        public Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.ConnectAsync(host, port, options, cancellationToken);
        }

        public void Connect(Uri uri, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Connect(uri, cancellationToken);
        }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.ConnectAsync(uri, cancellationToken);
        }

        public void Connect(string host, int port, bool useSsl, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Connect(host, port, useSsl, cancellationToken);
        }

        public Task ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.ConnectAsync(host, port, useSsl, cancellationToken);
        }

        public Task AuthenticateAsync(ICredentials credentials, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.AuthenticateAsync(credentials, cancellationToken);
        }

        public void Authenticate(string userName, string password, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Authenticate(userName, password, cancellationToken);
        }

        public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.AuthenticateAsync(userName, password, cancellationToken);
        }

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.DisconnectAsync(quit, cancellationToken);
        }

        public Task NoOpAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.NoOpAsync(cancellationToken);
        }

        public void Dispose()
        {
            _imapClient.Dispose();
        }

        public IProtocolLogger ProtocolLogger
        {
            get { return _imapClient.ProtocolLogger; }
        }

        public SslProtocols SslProtocols
        {
            get { return _imapClient.SslProtocols; }
            set { _imapClient.SslProtocols = value; }
        }

        public X509CertificateCollection ClientCertificates
        {
            get { return _imapClient.ClientCertificates; }
            set { _imapClient.ClientCertificates = value; }
        }

        public RemoteCertificateValidationCallback ServerCertificateValidationCallback
        {
            get { return _imapClient.ServerCertificateValidationCallback; }
            set { _imapClient.ServerCertificateValidationCallback = value; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _imapClient.LocalEndPoint; }
            set { _imapClient.LocalEndPoint = value; }
        }

        public event EventHandler<EventArgs> Connected
        {
            add { _imapClient.Connected += value; }
            remove { _imapClient.Connected -= value; }
        }

        public event EventHandler<EventArgs> Disconnected
        {
            add { _imapClient.Disconnected += value; }
            remove { _imapClient.Disconnected -= value; }
        }

        public event EventHandler<AuthenticatedEventArgs> Authenticated
        {
            add { _imapClient.Authenticated += value; }
            remove { _imapClient.Authenticated -= value; }
        }

        public Task EnableQuickResyncAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.EnableQuickResyncAsync(cancellationToken);
        }

        public Task<IEnumerable<IMailFolder>> GetFoldersAsync(FolderNamespace @namespace, bool subscribedOnly = false,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.GetFoldersAsync(@namespace, subscribedOnly, cancellationToken);
        }

        public Task<IMailFolder> GetFolderAsync(string path, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.GetFolderAsync(path, cancellationToken);
        }

        public event EventHandler<AlertEventArgs> Alert
        {
            add { _imapClient.Alert += value; }
            remove { _imapClient.Alert -= value; }
        }

        public void Compress(CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Compress(cancellationToken);
        }

        public Task CompressAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.CompressAsync(cancellationToken);
        }

        public void EnableQuickResync(CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.EnableQuickResync(cancellationToken);
        }

        public void EnableUTF8(CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.EnableUTF8(cancellationToken);
        }

        public Task EnableUTF8Async(CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.EnableUTF8Async(cancellationToken);
        }

        public ImapImplementation Identify(ImapImplementation clientImplementation,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.Identify(clientImplementation, cancellationToken);
        }

        public Task<ImapImplementation> IdentifyAsync(ImapImplementation clientImplementation,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.IdentifyAsync(clientImplementation, cancellationToken);
        }

        public void Authenticate(ICredentials credentials, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Authenticate(credentials, cancellationToken);
        }

        public void Connect(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
            CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Connect(host, port, options, cancellationToken);
        }

        public void Connect(Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
            CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Connect(socket, host, port, options, cancellationToken);
        }

        public void Disconnect(bool quit, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Disconnect(quit, cancellationToken);
        }

        public void NoOp(CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.NoOp(cancellationToken);
        }

        public void Idle(CancellationToken doneToken, CancellationToken cancellationToken = new CancellationToken())
        {
            _imapClient.Idle(doneToken, cancellationToken);
        }

        public Task IdleAsync(CancellationToken doneToken, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.IdleAsync(doneToken, cancellationToken);
        }

        public IMailFolder GetFolder(SpecialFolder folder)
        {
            return _imapClient.GetFolder(folder);
        }

        public IMailFolder GetFolder(FolderNamespace @namespace)
        {
            return _imapClient.GetFolder(@namespace);
        }

        public IMailFolder GetFolder(string path, CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.GetFolder(path, cancellationToken);
        }

        public IEnumerable<IMailFolder> GetFolders(FolderNamespace @namespace, bool subscribedOnly = false,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _imapClient.GetFolders(@namespace, subscribedOnly, cancellationToken);
        }

        public object SyncRoot
        {
            get { return _imapClient.SyncRoot; }
        }

        public ImapCapabilities Capabilities
        {
            get { return _imapClient.Capabilities; }
            set { _imapClient.Capabilities = value; }
        }

        public int InternationalizationLevel
        {
            get { return _imapClient.InternationalizationLevel; }
        }

        public AccessRights Rights
        {
            get { return _imapClient.Rights; }
        }

        public HashSet<string> AuthenticationMechanisms
        {
            get { return _imapClient.AuthenticationMechanisms; }
        }

        public HashSet<ThreadingAlgorithm> ThreadingAlgorithms
        {
            get { return _imapClient.ThreadingAlgorithms; }
        }

        public int Timeout
        {
            get { return _imapClient.Timeout; }
            set { _imapClient.Timeout = value; }
        }

        public bool IsConnected
        {
            get { return _imapClient.IsConnected; }
        }

        public bool IsAuthenticated
        {
            get { return _imapClient.IsAuthenticated; }
        }

        public bool IsIdle
        {
            get { return _imapClient.IsIdle; }
        }

        public FolderNamespaceCollection PersonalNamespaces
        {
            get { return _imapClient.PersonalNamespaces; }
        }

        public FolderNamespaceCollection SharedNamespaces
        {
            get { return _imapClient.SharedNamespaces; }
        }

        public FolderNamespaceCollection OtherNamespaces
        {
            get { return _imapClient.OtherNamespaces; }
        }

        public bool SupportsQuotas
        {
            get { return _imapClient.SupportsQuotas; }
        }

        public IMailFolder Inbox
        {
            get { return _imapClient.Inbox; }
        }

        public Task ConnectTask { get; set; }
        public Task AuthTask { get; set; }
        public Task InboxOpenTask { get; set; }
    }
}