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


        [TestInitialize]
        public void ImapMailBoxTestInitialization()
        {
            ImapClientDirector.Builder = ImapClientDirector.Builder.WithUserName(Settings.Default.TestUserName)
                .WithPassword(Settings.Default.TestPassword);

            _client = new Mock<IImapClient>();

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

            //setup the client's inbox
            _inbox = new Mock<IMailFolder>();
            _client.Setup(x => x.Inbox).Returns(_inbox.Object);
        }

        [TestMethod]
        public void TestMailBoxMessageReceived()
        {
            //setup the inbox, idler, and poller client
            var inbox = new ImapMailBox();
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


            var poller = new ImapPoller(pollerClient.Object);
            var idler = new ImapIdler(_client.Object);

            var eventWasDispatched = false;
            idler.MessageArrived += (sender, args) => { eventWasDispatched = true; };

            pvt.SetField("_imapIdler", idler);
            pvt.SetField("_imapPoller", poller);

            inbox.SetupEvent();

            Assert.AreEqual(idler, pvt.GetField("_imapIdler") as ImapIdler);
            Assert.AreEqual(poller, pvt.GetField("_imapPoller") as ImapPoller);

            //raise the event
            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));
            Assert.IsTrue(eventWasDispatched);



            pollerInbox.Verify(x => x.Fetch(
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<MessageSummaryItems>(), 
                It.IsAny<CancellationToken>()),
                "fetch was not called");

            //expect "testMessageId" in the results
            //Assert.AreEqual("testMessageId", inbox.EmailList[0]);
            Assert.IsTrue(inbox.EmailList.Count > 0);
        }
    }
}
