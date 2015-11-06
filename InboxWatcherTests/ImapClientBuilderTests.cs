using System.Threading;
using InboxWatcher;
using InboxWatcherTests.Properties;
using MailKit.Net.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InboxWatcherTests
{
    [TestClass]
    public class ImapClientBuilderTests
    {
        [TestMethod]
        public void GetImapClientTest()
        {
            var builder = new ImapClientBuilder();
            var client = builder.WithHost("outlook.office365.com")
                .WithUserName(Settings.Default.TestUserName)
                .WithPassword(Settings.Default.TestPassword).Build();

            if (!client.ConnectTask.IsCompleted) client.ConnectTask.Wait();
            if (!client.AuthTask.IsCompleted) client.AuthTask.Wait();
            if (!client.InboxOpenTask.IsCompleted) client.InboxOpenTask.Wait();
            
            Assert.AreEqual(true, client.IsConnected);
            Assert.AreEqual(true, client.IsAuthenticated);
            Assert.AreEqual(true, client.Inbox.IsOpen);
        }

        [TestMethod]
        public void GetImapClientReadyTest()
        {
            var builder = new ImapClientBuilder();
            var client = builder.WithHost("outlook.office365.com")
                .WithUserName(Settings.Default.TestUserName)
                .WithPassword(Settings.Default.TestPassword).BuildReady();

            Assert.AreEqual(true, client.IsConnected);
            Assert.AreEqual(true, client.IsAuthenticated);
            Assert.AreEqual(true, client.Inbox.IsOpen);
        }
    }
}