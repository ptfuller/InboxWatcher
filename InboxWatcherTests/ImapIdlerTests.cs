using System;
using System.Linq;
using System.Threading;
using InboxWatcher;
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
        public void TestMessageReceivedEventHandler()
        {
            var idle = new ImapIdler(_client.Object);
            idle.StartIdling();

            var eventWasDispatched = false;

            idle.MessageArrived += (sender, args) => { eventWasDispatched = true; };

            _inbox.Raise(x => x.MessagesArrived += null, new MessagesArrivedEventArgs(0));

            Assert.IsTrue(eventWasDispatched);
        }

        [TestMethod]
        public void TestTimerIdle()
        {
            var idle = new ImapIdler(_client.Object);
            idle.StartIdling();

            Assert.IsTrue(_client.Object.IsIdle);
            
            var timerPvt = new PrivateObject(idle);
            var timer = timerPvt.GetFieldOrProperty("_timeout") as System.Timers.Timer;
            
            Assert.IsTrue(timer.Enabled);
        }

        [TestMethod]
        public void TestIdleLoop()
        {
            var idle = new ImapIdler(_client.Object);
            var timerPvt = new PrivateObject(idle);

            idle.StartIdling();

            var timer = timerPvt.GetFieldOrProperty("_timeout") as System.Timers.Timer;
            var doneToken = timerPvt.GetFieldOrProperty("_doneToken") as CancellationTokenSource;

            var eventWasDispatched = false;

            timer.Elapsed += (sender, args) => { eventWasDispatched = true; };
            timer.Interval = 10;

            //setup moq object to no longer be idling
            _client.SetupGet(x => x.IsIdle).Returns(false);
            Assert.IsFalse(_client.Object.IsIdle);

            Thread.Sleep(20);

            //check that timer elapsed works
            Assert.IsTrue(eventWasDispatched);

            //check that doneToken cancelled
            Assert.IsTrue(doneToken.IsCancellationRequested);

            //get the new timer
            timer = timerPvt.GetFieldOrProperty("_timeout") as System.Timers.Timer;

            //check that elapsed reset
            Assert.IsTrue(timer.Interval == (9 * 60 * 1000));

            //check that new timer running
            Assert.IsTrue(timer.Enabled);

            //check that client is idling again
            Assert.IsTrue(_client.Object.IsIdle);

            //check that token is not cancelled
            doneToken = timerPvt.GetFieldOrProperty("_doneToken") as CancellationTokenSource;
            Assert.IsFalse(doneToken.IsCancellationRequested);
        }
    }
}
