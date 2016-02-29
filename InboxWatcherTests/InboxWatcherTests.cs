using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using Moq;
using Ninject;
using Ninject.Parameters;

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
            inboxWatcherPrivateObject.Invoke("ConfigureNinject");

            var task = (Task) inboxWatcherPrivateObject.Invoke("ConfigureMailBoxes");
            task.Wait(10000);

            Assert.IsTrue(InboxWatcher.MailBoxes.ContainsKey(1));
        }

        [TestMethod]
        public void TestKernel()
        {
            var kernel = (IKernel)inboxWatcherPrivateObject.Invoke("ConfigureNinject");

            var pvtType = new PrivateType(typeof(InboxWatcher));
            var configs = (List <ImapMailBoxConfiguration> ) pvtType.InvokeStatic("GetConfigs");
            
            var client = kernel.Get<ImapClientFactory>(new ConstructorArgument("configuration", configs[0]));
            var imapMailBox = client.GetMailBox();
            Debugger.Break();

        }
    }
}