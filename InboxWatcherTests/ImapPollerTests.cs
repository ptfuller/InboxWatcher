﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    public class ImapPollerTests
    {
        private Mock<IImapClient> _client;
        private Mock<IMailFolder> _inbox;

        private ImapClientDirector ImapClientDirector { get; set; }
        private ImapClientConfiguration _config;

        [TestInitialize]
        public void ImapPollerTestInitialization()
        {
            _client = new Mock<IImapClient>();

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ImapClientWrapper()));

            //setup the client's inbox
            _inbox = new Mock<IMailFolder>();
            _client.Setup(x => x.Inbox).Returns(_inbox.Object);

            _config = new ImapClientConfiguration()
            {
                HostName = "outlook.office365.com",
                Password = Settings.Default.TestPassword,
                Port = 993,
                UserName = Settings.Default.TestUserName,
                UseSecure = true
            };

            ImapClientDirector = new ImapClientDirector(_config);
        }


        [TestMethod]
        public void TestPollerGetsNewClientOnDisconnect()
        {
            var director = new Mock<ImapClientDirector>(_config);
            director.Setup(x => x.GetClient().Result).Returns(_client.Object);

            var idler = new ImapIdler(director.Object);

            var client2 = new Mock<IImapClient>();
            var client2Inbox = new Mock<IMailFolder>();
            client2.Setup(x => x.Inbox).Returns(client2Inbox.Object);

            idler.StartIdling();

            var poller = new ImapWorker(ImapClientDirector);

            client2.Raise(x => x.Disconnected += null, new EventArgs());

            var pvt = new PrivateObject(poller);
            var concreteClient = pvt.GetFieldOrProperty("ImapClient") as IImapClient;

            Assert.IsTrue(concreteClient.IsConnected);
            Assert.IsTrue(concreteClient.IsAuthenticated);
        }
    }
}
