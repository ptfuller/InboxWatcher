using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using MimeKit;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;

namespace InboxWatcher.ImapClient.Tests
{
    [TestClass()]
    public class ImapWorkerTests
    {

        private Mock<ImapClientConfiguration> _configuration;
        private Mock<IImapClient> client;
        private Mock<IMailFolder> inbox;
        private Mock<IImapFactory> factory;
        private IImapWorker _imapWorker;

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = new Mock<ImapClientConfiguration>();

            //mock client
            client = new Mock<IImapClient>();

            //mock inbox folder
            inbox = new Mock<IMailFolder>();

            //client returns mock inbox
            client.Setup(x => x.Inbox).Returns(inbox.Object);

            //mock factory
            factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetClient()).ReturnsAsync(client.Object);

            //create the idler and start setup
            _imapWorker = new ImapWorker(factory.Object);
        }

        [TestMethod()]
        public void GetMessageTest()
        {
            _imapWorker.Setup(false);

            //message
            var message = new MimeMessage();
            message.Subject = "GetMessageTest";

            inbox.Setup(x => x.GetMessageAsync(It.IsAny<UniqueId>(), It.IsAny<CancellationToken>(), null))
                .ReturnsAsync(message);

            var returnMessage = _imapWorker.GetMessage(new UniqueId(1)).Result;
            Assert.AreEqual("GetMessageTest", returnMessage.Subject);
        }

        [TestMethod()]
        public void GetMessageTestWithException()
        {
            _imapWorker.Setup(false);

            inbox.Setup(x => x.GetMessageAsync(It.IsAny<UniqueId>(), It.IsAny<CancellationToken>(), null))
                .ThrowsAsync(new ServiceNotConnectedException());

            try
            {
                var result = _imapWorker.GetMessage(new UniqueId(1)).Result;
                Assert.Fail("Exception not thrown");
            }
            catch (Exception ex)
            {

            }
        }

        [TestMethod()]
        public void DeleteMessageTest()
        {
            _imapWorker.Setup(false);
            _imapWorker.DeleteMessage(new UniqueId(1));
            inbox.Verify(x => x.ExpungeAsync(It.IsAny<CancellationToken>()));
        }

        [TestMethod()]
        public void GetNewMessagesTest()
        {
            _imapWorker.Setup(false);

            var fix = new Fixture();
            fix.Customize(new AutoMoqCustomization());

            var messages = fix.CreateMany<IMessageSummary>().ToList();

            inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(messages);

            var newMessages = _imapWorker.GetNewMessages(2).Result;

            Assert.AreEqual(messages.First(), newMessages.First());
        }

        [TestMethod()]
        public void FreshenMailBoxTest()
        {
            Assert.Fail();
        }

        //[TestMethod()]
        //public void GetMessageSummaryUidTest()
        //{
        //    _imapWorker.Setup(false);

        //    //mock message summary has an index of 1
        //    var msgSummary = new Mock<IMessageSummary>();
        //    msgSummary.Setup(x => x.Index).Returns(1);

        //    var msgSummaryList = new List<IMessageSummary> { msgSummary.Object };

        //    inbox.Setup(
        //        x =>
        //            x.FetchAsync(It.IsAny<List<UniqueId>>(), It.IsAny<MessageSummaryItems>(),
        //                It.IsAny<CancellationToken>())).ReturnsAsync(msgSummaryList);

        //    //fake uid
        //    var uid = new UniqueId(1);

        //    var summary = _imapWorker.GetMessageSummary(uid).Result;

        //    //summary should have the index of 1
        //    Assert.AreEqual(1, summary.Index);
        //}
    }
}