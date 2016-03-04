using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Moq;

namespace InboxWatcher.ImapClient.Tests
{
    [TestClass()]
    public class ImapIdlerTests
    {
        private Mock<ImapClientConfiguration> _configuration;

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = new Mock<ImapClientConfiguration>();
        }

        [TestMethod()]
        public void ImapIdlerConstructorTest()
        {
            var factory = new Mock<ImapClientFactory>(_configuration.Object);
            var idler = new ImapIdler(factory.Object);

            var imapIdlerPvtObject = new PrivateObject(idler);

            Assert.AreEqual(factory.Object, imapIdlerPvtObject.GetFieldOrProperty("Factory"));
            Assert.AreNotEqual(null, imapIdlerPvtObject.GetFieldOrProperty("Timeout"));
            Assert.AreNotEqual(null, imapIdlerPvtObject.GetFieldOrProperty("IntegrityCheckTimer"));
        }

        [TestMethod()]
        public void SetupTest()
        {
            //mock client
            var client = new Mock<IImapClient>();

            //mock inbox folder
            var inbox = new Mock<IMailFolder>();

            //client returns mock inbox
            client.Setup(x => x.Inbox).Returns(inbox.Object);
            
            //mock factory
            var factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetClient()).ReturnsAsync(client.Object);

            //create the idler and start setup
            var imapIdler = new ImapIdler(factory.Object);
            
            //mock trace listener
            var trace = new Mock<TraceListener>();
            Trace.Listeners.Add(trace.Object);

            imapIdler.Setup(false).Wait();

            //verify the factory was asked for the client
            factory.Verify(x => x.GetClient());

            //verify setup events working - disconnected
            client.Raise(x => x.Disconnected += null, new EventArgs());
            trace.Verify(x => x.WriteLine("ImapClient disconnected"));

            //verify setup events working - inbox opened
            inbox.Raise(x => x.Opened += null, new EventArgs());
            trace.Verify(x => x.WriteLine(It.Is<string>(z => z.Contains("Inbox opened"))));

            //verify setup events working - inbox closed
            inbox.Raise(x => x.Closed += null, new EventArgs());
            trace.Verify(x => x.WriteLine(It.Is<string>(z => z.Contains("Inbox closed"))));
        }

        [TestMethod]
        public void HandleExceptionTest()
        {
            var factory = new Mock<IImapFactory>();
            var idler = new ImapIdler(factory.Object);

            var eventCalled = false;
            idler.ExceptionHappened += (sender, args) => eventCalled = true;

            var pvtObject = new PrivateObject(idler);
            pvtObject.Invoke("HandleException", new object[] {new Exception("test exception"), false});

            Assert.IsTrue(eventCalled);
        }
    }
}