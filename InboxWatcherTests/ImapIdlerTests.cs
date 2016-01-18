using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using InboxWatcherTests.Properties;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using Moq;

namespace InboxWatcherTests
{
    [TestClass]
    public class ImapIdlerTests
    {
        private Mock<IImapClient> _client;
        private Mock<IMailFolder> _inbox;
        private ImapClientConfiguration _config;

        private ImapClientDirector ImapClientDirector { get; set; }

        [TestInitialize]
        public void ImapIdlerTestInitialization()
        {
            _client = new Mock<IImapClient>();
            _client.Name = "ImapIdlerTestInitialization Mock Imap Client";

            //return true for IsIdle if we've asked the client to idle
            _client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Callback(() => _client.SetupGet(x => x.IsIdle).Returns(true));

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
        public void TestStartIdling()
        {
            var director = new Mock<ImapClientDirector>(_config);
            director.Setup(x => x.GetClient().Result).Returns(_client.Object);

            var idle = new ImapIdler(director.Object);

            idle.StartIdling();

            Assert.IsTrue(_client.Object.IsIdle);
        }

        [TestMethod]
        public void TestMessageReceivedEventHandler()
        {
            var director = new Mock<ImapClientDirector>(_config);
            director.Setup(x => x.GetClient().Result).Returns(_client.Object);

            var idle = new ImapIdler(director.Object);
            idle.StartIdling();

            var eventWasDispatched = false;

            idle.MessageArrived += (sender, args) => { eventWasDispatched = true; };

            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));

            Assert.IsTrue(eventWasDispatched);
        }

        [TestMethod]
        public void TestTimerIdle()
        {
            var director = new Mock<ImapClientDirector>(_config);
            director.Setup(x => x.GetClient().Result).Returns(_client.Object);

            var idle = new ImapIdler(director.Object);
            idle.StartIdling();

            Assert.IsTrue(_client.Object.IsIdle);
            
            var timerPvt = new PrivateObject(idle);
            var timer = timerPvt.GetFieldOrProperty("Timeout") as System.Timers.Timer;
            
            Assert.IsTrue(timer.Enabled);
        }

    }
}
