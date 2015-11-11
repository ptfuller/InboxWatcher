﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using InboxWatcher;
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

        [TestInitialize]
        public void ImapPollerTestInitialization()
        {
            _client = new Mock<IImapClient>();

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

            //setup the client's inbox
            _inbox = new Mock<IMailFolder>();
            _client.Setup(x => x.Inbox).Returns(_inbox.Object);

            ImapClientDirector.Builder = ImapClientDirector.Builder.WithUserName(Settings.Default.TestUserName)
                .WithPassword(Settings.Default.TestPassword);
        }

        [TestMethod]
        public void TestPollerGetsCallFromIdleLoop()
        {
            var inbox = new ImapMailBox();
            var pvt = new PrivateObject(inbox);

            var idler = new ImapIdler(_client.Object);
            pvt.SetField("_imapIdler", idler);

            Assert.AreEqual(idler, pvt.GetFieldOrProperty("_imapIdler") as ImapIdler);

            var client2 = new Mock<IImapClient>();
            var client2Inbox = new Mock<IMailFolder>();
            client2.Setup(x => x.Inbox).Returns(client2Inbox.Object);

            idler.StartIdling();

            var poller = new ImapPoller(client2.Object);
            pvt.SetField("_imapPoller", poller);

            Assert.AreEqual(poller, pvt.GetFieldOrProperty("_imapPoller") as ImapPoller);

            inbox.SetupEvent();

            //raise message arrived event in the idler
            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));

            //verify that poller fetches messages
            client2Inbox.Verify(x => x.Fetch(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(), It.IsAny<CancellationToken>()),
                "Fetch was not called");
        }

        [TestMethod]
        public void TestPollerGetsNewClientOnDisconnect()
        {
            var idler = new ImapIdler(_client.Object);

            var client2 = new Mock<IImapClient>();
            var client2Inbox = new Mock<IMailFolder>();
            client2.Setup(x => x.Inbox).Returns(client2Inbox.Object);

            idler.StartIdling();

            var poller = new ImapPoller(client2.Object);

            client2.Raise(x => x.Disconnected += null, new EventArgs());

            var pvt = new PrivateObject(poller);
            var concreteClient = pvt.GetFieldOrProperty("_client") as IImapClient;

            Assert.IsTrue(concreteClient.IsConnected);
            Assert.IsTrue(concreteClient.IsAuthenticated);
        }
    }
}
