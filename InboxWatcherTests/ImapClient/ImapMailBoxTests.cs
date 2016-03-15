using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using MailKit.Net.Imap;
using Moq;
using Ninject;
using Ninject.Parameters;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;

namespace InboxWatcher.ImapClient.Tests
{
    [TestClass()]
    public class ImapMailBoxTests
    {
        private IImapMailBox _imapMailBox;
        private Mock<IClientConfiguration> configuration;
        private Mock<IImapFactory> factory;
        private Mock<IEmailSender> _sender = new Mock<IEmailSender>();
        private Mock<IImapWorker> _worker = new Mock<IImapWorker>();
        private Mock<IImapIdler> _idler = new Mock<IImapIdler>();
        private Mock<IMailBoxLogger> _logger = new Mock<IMailBoxLogger>();
        private IKernel _kernel;

        [TestInitialize]
        public void TestInitialize()
        {
            configuration = new Mock<IClientConfiguration>();

            factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetConfiguration()).Returns(configuration.Object);

            _kernel = new StandardKernel();

            _kernel.Bind<IClientConfiguration>().To<ImapClientConfiguration>();
            _kernel.Bind<IImapMailBox>().ToProvider(new ImapMailBoxProvider());
            _kernel.Bind<IImapFactory>().ToConstant(factory.Object);
            _kernel.Bind<IMailBoxLogger>().ToConstant(_logger.Object);
            _kernel.Bind<IImapWorker>().ToConstant(_worker.Object);
            _kernel.Bind<IImapIdler>().ToConstant(_idler.Object);
            _kernel.Bind<IEmailSender>().ToConstant(_sender.Object);
            _kernel.Bind<IEmailFilterer>().To<EmailFilterer>();

            _imapMailBox = _kernel.Get<IImapMailBox>(new ConstructorArgument("config", configuration.Object));
        }

        [TestMethod()]
        public void SetupTest()
        {
            //arrange
            var pvt = new PrivateObject(_imapMailBox);

            //act
            _imapMailBox.Setup().Wait();

            //assert
            var result = (bool)pvt.GetProperty("_setupInProgress");
            Assert.IsFalse(result);
            _worker.Verify(x => x.GetMailFolders());
        }

        [TestMethod()]
        public void SetupClientsTest()
        {
            var pvt = new PrivateObject(_imapMailBox);

            pvt.SetField("_imapIdler", _idler.Object);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            var result = (Task<bool>) pvt.Invoke("SetupClients");

            _sender.Verify(x => x.Setup());
            _idler.Verify(x => x.Setup(It.Is<bool>(y => y == false)));
            _worker.Verify(x => x.Setup(It.Is<bool>(y => y == false)));
            
            Assert.IsTrue(result.Result);
        }

        [TestMethod()]
        public void SetupClientsWithException()
        {
            //setup sender to throw an exception
            _sender.Setup(x => x.Setup()).Throws(new IOException());

            var pvt = new PrivateObject(_imapMailBox);
            var result = (Task<bool>)pvt.Invoke("SetupClients");

            //exception added to list
            Assert.AreEqual(1, _imapMailBox.Exceptions.Count);
            //return false
            Assert.IsFalse(result.Result);
        }

        [TestMethod()]
        public void FreshenMailBoxTest()
        {
            //setup private object
            var pvt = new PrivateObject(_imapMailBox);
            
            pvt.SetFieldOrProperty("_imapWorker", _worker.Object);

            //* start the freshen *//
            var result = (Task<bool>)pvt.Invoke("FreshenMailBox");

            _worker.Verify(x => x.FreshenMailBox(It.IsAny<string>()));
            Assert.IsTrue(result.Result);
            Assert.IsFalse((bool) pvt.GetFieldOrProperty("_freshening"));
        }

        [TestMethod()]
        public void FreshenMailBoxTestWithException()
        {
            //setup fake imapworker
            var _configuration = new Mock<ImapClientConfiguration>();

            //mock client
            var client = new Mock<IImapClient>();

            //mock inbox folder
            var inbox = new Mock<IMailFolder>();

            //client returns mock inbox
            client.Setup(x => x.Inbox).Returns(inbox.Object);

            //mock factory
            factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetClient()).ReturnsAsync(client.Object);

            //create the idler and start setup
            var _imapWorker = new ImapWorker(factory.Object);

            //setup private object
            var pvt = new PrivateObject(_imapMailBox);
            pvt.SetFieldOrProperty("_imapWorker", _imapWorker);

            var r = new ImapCommandResponse();

            //first fetch throws exception - after closing and opening folders now messages are returned
            //this mirrors how the client has been functioning in real world tests
            inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ImapCommandException(r,
                    "The IMAP server replied to the 'FETCH' command with a 'NO' response."));

            //* start the freshen *//
            var result = (Task<bool>)pvt.Invoke("FreshenMailBox");

            Assert.IsFalse(result.Result);
            factory.Verify(x => x.GetClient(), Times.Exactly(2));
        }

        [TestMethod()]
        public void SetupEventsTests()
        {
            var pvt = new PrivateObject(_imapMailBox);

            pvt.SetField("_imapIdler", _idler.Object);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            var result = (bool) pvt.Invoke("SetupEvents");

            Assert.IsTrue(result);
        }

        [TestMethod()]
        public void SetupEventsTestWithException()
        {
            var pvt = new PrivateObject(_imapMailBox);

            //null object should throw exception on event assignment
            pvt.SetField("_imapIdler", null);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            var result = (bool)pvt.Invoke("SetupEvents");

            Assert.IsFalse(result);
        }

        [TestMethod()]
        public void IdlerMessageArrivedEventTest()
        {
            //arrange
            //subscribe the events
            var pvt = new PrivateObject(_imapMailBox);

            pvt.SetField("_imapIdler", _idler.Object);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            pvt.Invoke("SetupEvents");
            
            //act
            _idler.Raise(x => x.MessageArrived += null, new MessagesArrivedEventArgs(1));
            Task.Delay(3000).Wait();

            //assert
            //worker should get call to get new message
            _worker.Verify(x => x.GetNewMessages(It.Is<int>(i => i == 1)));
        }

        [TestMethod()]
        public void IdlerMessageExpungedEventTest()
        {
            //arrange
            //subscribe the events
            var pvt = new PrivateObject(_imapMailBox);

            pvt.SetField("_imapIdler", _idler.Object);
            pvt.SetField("_imapWorker", _worker.Object);
            pvt.SetField("_emailSender", _sender.Object);

            var msgSummary = new Mock<IMessageSummary>();

            var emailList = new Mock<IList<IMessageSummary>>();
            emailList.Setup(x => x.Count).Returns(2);
            emailList.SetupGet(x => x[1]).Returns(msgSummary.Object);
            _imapMailBox.EmailList = emailList.Object;

            //act
            pvt.Invoke("SetupEvents");
            _idler.Raise(x => x.MessageExpunged += null, new MessageEventArgsWrapper(1));

            //assert
            emailList.Verify(x => x.Count);
            emailList.Verify(x => x[1]);
        }
    }
}