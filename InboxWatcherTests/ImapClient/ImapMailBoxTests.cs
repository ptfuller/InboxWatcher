using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Moq;

namespace InboxWatcher.ImapClient.Tests
{
    [TestClass()]
    public class ImapMailBoxTests
    {
        private ImapMailBox _imapMailBox;
        private Mock<IClientConfiguration> configuration;
        private Mock<IImapFactory> factory;
        private Mock<IEmailSender> _sender = new Mock<IEmailSender>();
        private Mock<IImapWorker> _worker = new Mock<IImapWorker>();
        private Mock<IImapIdler> _idler = new Mock<IImapIdler>();

        [TestInitialize]
        public void TestInitialize()
        {
            configuration = new Mock<IClientConfiguration>();

            factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetConfiguration()).Returns(configuration.Object);

            _imapMailBox = new ImapMailBox(configuration.Object, factory.Object);

            factory.Setup(x => x.GetEmailSender()).Returns(_sender.Object);
            factory.Setup(x => x.GetImapIdler()).Returns(_idler.Object);
            factory.Setup(x => x.GetImapWorker()).Returns(_worker.Object);
        }

        [TestMethod()]
        public void SetupTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void SetupClientsTest()
        {
            var pvt = new PrivateObject(_imapMailBox);

            pvt.SetField("_imapIdler", _idler.Object);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            var result = (Task<bool>) pvt.Invoke("SetupClients");

            factory.Verify(x => x.GetImapWorker());
            factory.Verify(x => x.GetImapIdler());
            factory.Verify(x => x.GetEmailSender());

            _sender.Verify(x => x.Setup());
            _idler.Verify(x => x.Setup(It.Is<bool>(y => y == false)));
            _worker.Verify(x => x.Setup(It.Is<bool>(y => y == false)));

            var emailListMock = new Mock<IList<IMessageSummary>>();
            emailListMock.Setup(x => x.Count).Returns(2);
            _imapMailBox.EmailList = emailListMock.Object;

            _idler.Raise(x => x.MessageExpunged += null, new EventArgs());

            emailListMock.Verify(x => x[It.IsAny<int>()], Times.Never);

            Assert.IsTrue(result.Result);
        }

        [TestMethod()]
        public void SetupClientsWithException()
        {
            //setup idler to throw an exception
            factory.Setup(x => x.GetImapIdler()).Throws(new IOException());

            var pvt = new PrivateObject(_imapMailBox);
            var result = (Task<bool>)pvt.Invoke("SetupClients");

            //exception added to list
            Assert.AreEqual(1, _imapMailBox.Exceptions.Count);
            //return false
            Assert.IsFalse(result.Result);
        }
    }
}