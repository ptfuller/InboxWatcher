using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.WebAPI.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InboxWatcher.WebAPI.Controllers.Tests
{
    [TestClass()]
    public class NotificationControllerTests
    {
        [TestInitialize]
        public void TestInit()
        {
            controller = new NotificationController();
        }

        private NotificationController controller;

        [TestMethod()]
        public void GetNotificationConfigurationsTest()
        {
            var result = controller.GetNotificationConfigurations();

            using (var ctx = new MailModelContainer())
            {
                Assert.AreEqual(ctx.NotificationConfigurations.Count(), result.Count());
            }
        }

        [TestMethod()]
        public void GetNotificationConfigurationsWithName()
        {
            var result = controller.GetNotificationConfigurations("TestMailBox");

            using (var ctx = new MailModelContainer())
            {
                Assert.AreEqual(ctx.NotificationConfigurations.Count(x => x.ImapMailBoxConfiguration.MailBoxName.Equals("TestMailBox")), result.Count());
            }
        }
    }
}