using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using InboxWatcher;
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
            var inbox = new ImapMailBox(ImapClientDirector.Object);
            var pvt = new PrivateObject(inbox);

            var pollerInbox = new Mock<IMailFolder>();
            pollerInbox.Name = "Poller Inbox";
            var pollerClient = new Mock<IImapClient>();
            

            //setup the IMessageSummary
            var testSummary = new List<IMessageSummary>();
            var summaryMock = new Mock<IMessageSummary>();

            testSummary.Add(summaryMock.Object);

            //inbox returns the testSummary
            pollerInbox.Setup(x => x.Fetch(
                It.IsAny<int>(),
                It.IsAny<int>(), 
                It.IsAny<MessageSummaryItems>(), 
                It.IsAny<CancellationToken>()))
                .Returns(testSummary);

            pollerClient.Setup(x => x.Inbox).Returns(pollerInbox.Object);

            var poller = pvt.GetFieldOrProperty("_imapPoller") as ImapWorker;
            var idler = pvt.GetFieldOrProperty("_imapIdler") as ImapIdler;

            var pollerPvt = new PrivateObject(poller);
            pollerPvt.SetFieldOrProperty("ImapClient", pollerClient.Object);

            var eventWasDispatched = false;
            idler.MessageArrived += (sender, args) => { eventWasDispatched = true; };

            //set the fields of the ImapMailBox
            pvt.SetField("_imapIdler", idler);
            pvt.SetField("_imapPoller", poller);

            //reset the event handler
            inbox.Setup();

            //make sure the fields were set correctly
            Assert.AreEqual(idler, pvt.GetField("_imapIdler") as ImapIdler);
            Assert.AreEqual(poller, pvt.GetField("_imapPoller") as ImapWorker);

            //raise the event
            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));
            Assert.IsTrue(eventWasDispatched);

            //poller should be called
            pollerInbox.Verify(x => x.Fetch(
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<MessageSummaryItems>(), 
                It.IsAny<CancellationToken>()),
                "fetch was not called");

            //mailbox should now have an email
            Assert.IsTrue(inbox.EmailList.Count > 0);
        }
    }
}
