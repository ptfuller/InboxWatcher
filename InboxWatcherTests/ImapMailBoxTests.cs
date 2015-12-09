using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using InboxWatcherTests.Properties;
using MailKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace InboxWatcherTests
{
    [TestClass]
    public class ImapMailBoxTests
    {
        private Mock<IImapClient> _client;
        private Mock<IMailFolder> _inbox;

        private Mock<ImapClientDirector> ImapClientDirector { get; set; }

        [TestInitialize]
        public void ImapMailBoxTestInitialization()
        {
            var config = new ImapClientConfiguration()
            {
                HostName = "outlook.office365.com",
                Password = Settings.Default.TestPassword,
                Port = 993,
                UserName = Settings.Default.TestUserName,
                UseSecure = true
            };

            ImapClientDirector = new Mock<ImapClientDirector>(config);

            _client = new Mock<IImapClient>();

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ImapClientAdapter()));

            //setup the client's inbox
            _inbox = new Mock<IMailFolder>();
            _client.Setup(x => x.Inbox).Returns(_inbox.Object);

            ImapClientDirector.Setup(x => x.GetReadyClient()).Returns(_client.Object);
            ImapClientDirector.Setup(x => x.GetClient()).Returns(_client.Object);
            ImapClientDirector.Setup(x => x.GetThisClientReady(It.IsAny<IImapClient>())).Returns(_client.Object);
        }

        [TestMethod]
        public void TestMailBoxMessageReceived()
        {
            //setup the inbox, idler, and poller client
            var mailbox = new ImapMailBox(ImapClientDirector.Object, new ImapClientConfiguration());

            //raise message received
            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));

            //expect worker to fetch
            _inbox.Verify(x => x.Fetch(It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<MessageSummaryItems>(), It.IsAny<CancellationToken>()));
        }
    }
}
