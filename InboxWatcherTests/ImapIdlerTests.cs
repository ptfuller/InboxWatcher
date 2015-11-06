using System;
using System.Linq;
using System.Threading;
using InboxWatcher;
using InboxWatcherTests.Properties;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace InboxWatcherTests
{
    [TestClass]
    public class ImapIdlerTests
    {
        private Mock<IImapClient> _client;
        private Mock<IMailFolder> _inbox;

        [TestInitialize]
        public void ImapIdlerTestInitialization()
        {
            _client = new Mock<IImapClient>();

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

            //setup the client's inbox
            _inbox = new Mock<IMailFolder>();
            _client.Setup(x => x.Inbox).Returns(_inbox.Object);
        }

        [TestMethod]
        public void TestStartIdling()
        {
            var idle = new ImapIdler(_client.Object);

            idle.StartIdling();

            Assert.IsTrue(_client.Object.IsIdle);
        }

        [TestMethod]
        public void TestMessageReceived()
        {
            var idle = new ImapIdler(_client.Object);
            idle.StartIdling();

            var eventWasDispatched = false;

            idle.MessageArrived += (sender, args) => { eventWasDispatched = true; };

            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));

            Assert.IsTrue(eventWasDispatched);
        }
    }
}
