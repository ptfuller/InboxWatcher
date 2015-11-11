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
    public interface IImapClient
    {
        object SyncRoot { get; }

        ImapCapabilities Capabilities { get; set; }
        int InternationalizationLevel { get; }
        AccessRights Rights { get; }
        HashSet<string> AuthenticationMechanisms { get; }
        HashSet<ThreadingAlgorithm> ThreadingAlgorithms { get; }
        int Timeout { get; set; }
        bool IsConnected { get; }
        bool IsAuthenticated { get; }
        bool IsIdle { get; }
        FolderNamespaceCollection PersonalNamespaces { get; }
        FolderNamespaceCollection SharedNamespaces { get; }
        FolderNamespaceCollection OtherNamespaces { get; }
        bool SupportsQuotas { get; }
        IMailFolder Inbox { get; }
        IProtocolLogger ProtocolLogger { get; }
        SslProtocols SslProtocols { get; set; }
        X509CertificateCollection ClientCertificates { get; set; }
        RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }
        IPEndPoint LocalEndPoint { get; set; }

        Task ConnectTask { get; set; }
        Task AuthTask { get; set; }
        Task InboxOpenTask { get; set; }


        void Compress(CancellationToken cancellationToken);
        Task CompressAsync(CancellationToken cancellationToken);
        void EnableQuickResync(CancellationToken cancellationToken);
        void EnableUTF8(CancellationToken cancellationToken);
        Task EnableUTF8Async(CancellationToken cancellationToken);
        ImapImplementation Identify(ImapImplementation clientImplementation, CancellationToken cancellationToken);

        Task<ImapImplementation> IdentifyAsync(ImapImplementation clientImplementation,
            CancellationToken cancellationToken);

        void Authenticate(ICredentials credentials, CancellationToken cancellationToken);

        void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken);

        void Connect(Socket socket, string host, int port, SecureSocketOptions options,
            CancellationToken cancellationToken);

        void Disconnect(bool quit, CancellationToken cancellationToken);
        void NoOp(CancellationToken cancellationToken);
        void Idle(CancellationToken doneToken, CancellationToken cancellationToken);
        Task IdleAsync(CancellationToken doneToken, CancellationToken cancellationToken);
        IMailFolder GetFolder(SpecialFolder folder);
        IMailFolder GetFolder(FolderNamespace @namespace);
        IMailFolder GetFolder(string path, CancellationToken cancellationToken);


        IEnumerable<IMailFolder> GetFolders(FolderNamespace @namespace, bool subscribedOnly,
            CancellationToken cancellationToken);

        Task EnableQuickResyncAsync(CancellationToken cancellationToken);

        Task<IEnumerable<IMailFolder>> GetFoldersAsync(FolderNamespace @namespace, bool subscribedOnly,
            CancellationToken cancellationToken);

        Task<IMailFolder> GetFolderAsync(string path, CancellationToken cancellationToken);

        event EventHandler<AlertEventArgs> Alert;
        Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken);
        void Connect(Uri uri, CancellationToken cancellationToken);
        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        void Connect(string host, int port, bool useSsl, CancellationToken cancellationToken);
        Task ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken);
        Task AuthenticateAsync(ICredentials credentials, CancellationToken cancellationToken);
        void Authenticate(string userName, string password, CancellationToken cancellationToken);
        Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);
        Task DisconnectAsync(bool quit, CancellationToken cancellationToken);
        Task NoOpAsync(CancellationToken cancellationToken);

        void Dispose();
        event EventHandler<EventArgs> Connected;
        event EventHandler<EventArgs> Disconnected;
        event EventHandler<AuthenticatedEventArgs> Authenticated;
    }
}