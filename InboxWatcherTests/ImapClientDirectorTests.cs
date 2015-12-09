using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InboxWatcher.ImapClient;
using InboxWatcherTests.Properties;

namespace InboxWatcherTests
{
    [TestClass]
    public class ImapClientDirectorTests
    {
        private ImapClientDirector ImapClientDirector { get; set; }

        [TestInitialize]
        public void InitializeImapClientDirectorTests()
        {
            var config = new ImapClientConfiguration()
            {
                HostName = "outlook.office365.com",
                Password = Settings.Default.TestPassword,
                Port = 993,
                UserName = Settings.Default.TestUserName,
                UseSecure = true
            };

            ImapClientDirector = new ImapClientDirector(config);
        }

        [TestMethod]
        public void GetClientTest()
        {
            var clientType = ImapClientDirector.GetClient().GetType();
            Assert.AreEqual(typeof(ImapClientAdapter), clientType);
        }

        [TestMethod]
        public void GetThisClientReadyTest()
        {
            var client = ImapClientDirector.GetClient();
            Assert.IsFalse(client.IsAuthenticated);
            Assert.IsTrue(ImapClientDirector.GetThisClientReady(client).IsAuthenticated);
        }
    }
}