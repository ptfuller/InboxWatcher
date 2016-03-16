using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using InboxWatcher;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ninject;
using Ninject.Parameters;

namespace InboxWatcherTests
{
    [TestClass()]
    public class InboxWatcherTests
    {
        private PrivateObject inboxWatcherPrivateObject;
        
        [TestInitialize]
        public void TestInitialize()
        {
            var ibxWatcher = new InboxWatcher.InboxWatcher();
            inboxWatcherPrivateObject = new PrivateObject(ibxWatcher);
        }

        [TestMethod]
        public void TestGetConfigs()
        {
            inboxWatcherPrivateObject.Invoke("Setup");
            Assert.IsTrue(InboxWatcher.InboxWatcher.GetConfigs().Count > 0);
        }

        [TestMethod]
        public void TestConfigureMailBoxes()
        {
            inboxWatcherPrivateObject.Invoke("ConfigureNinject");

            var task = (Task) inboxWatcherPrivateObject.Invoke("ConfigureMailBoxes");
            task.Wait(10000);

            Assert.IsTrue(InboxWatcher.InboxWatcher.MailBoxes.ContainsKey(1));
        }

        [TestMethod]
        public void TestKernel()
        {
            var kernel = (IKernel)inboxWatcherPrivateObject.Invoke("ConfigureNinject");

            var pvtType = new PrivateType(typeof(InboxWatcher.InboxWatcher));
            var configs = (List <ImapMailBoxConfiguration> ) pvtType.InvokeStatic("GetConfigs");
            
            var client = kernel.Get<IImapFactory>(new ConstructorArgument("configuration", configs[0]));
            var imapMailBox = kernel.Get<IImapMailBox>(new ConstructorArgument("config", configs[0]));
            Debugger.Break();
        }
    }
}