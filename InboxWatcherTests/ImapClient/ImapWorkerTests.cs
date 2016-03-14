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
using MailKit.Net.Imap;
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
            _imapWorker.Setup(false);

            var fix = new Fixture();
            fix.Customize(new AutoMoqCustomization());

            var messages = fix.CreateMany<IMessageSummary>().ToList();

            inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(messages);

            var results = _imapWorker.FreshenMailBox().Result;

            Assert.AreEqual(messages.Count, results.Count());
        }

        [TestMethod()]
        public void FreshenMailBoxTestWithException()
        {
            _imapWorker.Setup(false);

            var r = new ImapCommandResponse();

            var fix = new Fixture();
            fix.Customize(new AutoMoqCustomization());

            var messages = fix.CreateMany<IMessageSummary>().ToList();

            //first fetch throws exception - after closing and opening folders now messages are returned
            //this mirrors how the client has been functioning in real world tests
            inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                It.IsAny<CancellationToken>())).ThrowsAsync(new ImapCommandException(r ,"The IMAP server replied to the 'FETCH' command with a 'NO' response."))
                .Callback(() =>
                {
                    inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                        It.IsAny<CancellationToken>())).ReturnsAsync(messages);
                });

            _imapWorker.FreshenMailBox();

            inbox.Verify(x => x.CloseAsync(It.Is<bool>(y => y == false), It.IsAny<CancellationToken>()), Times.Exactly(1));
            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        }

        [TestMethod()]
        [ExpectedException(typeof(AggregateException))]
        public void FreshenMailBoxTestWithMultipleExceptions()
        {
            _imapWorker.Setup(false);

            var r = new ImapCommandResponse();

            var fix = new Fixture();
            fix.Customize(new AutoMoqCustomization());

            var messages = fix.CreateMany<IMessageSummary>().ToList();
            
            //always throw - make sure we can't get stuck
            inbox.Setup(x => x.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MessageSummaryItems>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ImapCommandException(r,
                    "The IMAP server replied to the 'FETCH' command with a 'NO' response."));

            var result = _imapWorker.FreshenMailBox().Result;

            inbox.Verify(x => x.CloseAsync(It.Is<bool>(y => y == false), It.IsAny<CancellationToken>()), Times.Exactly(1));
            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        }
    }
}