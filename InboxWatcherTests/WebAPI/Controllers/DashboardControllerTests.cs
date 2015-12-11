using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.WebAPI.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InboxWatcher.WebAPI.Controllers.Tests
{
    [TestClass()]
    public class DashboardControllerTests
    {
        [TestInitialize]
        public void TestInit()
        {
            dc = new DashboardController();
        }

        private DashboardController dc;

        [TestMethod()]
        public void GetTest()
        {
            InboxWatcher.ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
            var response = dc.Get();
            
            Assert.AreEqual("text/html", response.Content.Headers.ContentType.ToString());
        }
    }
}