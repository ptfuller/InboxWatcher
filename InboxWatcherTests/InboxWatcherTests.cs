using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.ImapClient;

namespace InboxWatcher.Tests
{
    [TestClass()]
    public class InboxWatcherTests
    {
        private PrivateObject inboxWatcherPrivateObject;

        [TestInitialize]
        public void TestInitialize()
        {
            var ibxWatcher = new InboxWatcher();
            inboxWatcherPrivateObject = new PrivateObject(ibxWatcher);
        }

        [TestMethod]
        public void TestGetConfigs()
        {
            inboxWatcherPrivateObject.Invoke("Setup");
            Assert.IsTrue(InboxWatcher.GetConfigs().Count > 0);
        }

        [TestMethod]
        public void TestConfigureMailBoxes()
        {
            var result = (Task) inboxWatcherPrivateObject.Invoke("Setup");
            result.Wait();
            Assert.IsTrue(InboxWatcher.MailBoxes.ContainsKey(1));
        }
    }
}