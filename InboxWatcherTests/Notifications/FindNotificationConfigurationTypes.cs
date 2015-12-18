using System;
using System.Collections.Generic;
using System.Linq;
using InboxWatcher.WebAPI.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InboxWatcher.Notifications.Tests
{
    [TestClass]
    public class FindNotificationConfigurationTypes
    {
        [TestMethod]
        public void FindNotificationConfigurationTypesTest()
        {
            var cc = new NotificationController();
            var pvt = new PrivateObject(cc);

            var results = (IEnumerable<Type>)pvt.Invoke("FindNotificationConfigurationTypes");
            Assert.AreEqual(2, results.Count());
        } 
    }
}