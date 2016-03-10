using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using InboxWatcher.Tests;
using MailKit;
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

            _worker.Verify(x => x.FreshenMailBox());
            Assert.IsTrue(result.Result);
            Assert.IsFalse((bool) pvt.GetFieldOrProperty("_freshening"));
        }
    }
}