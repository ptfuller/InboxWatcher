using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace InboxWatcher.DTO.Tests
{
    [TestClass()]
    public class EmailLogDtoTests
    {
        [TestMethod()]
        public void EmailLogDtoTest()
        {
            var email = new Mock<Email>();
            var el = new EmailLog()
            {
                Action = "TestAction",
                Email = email.Object,
                EmailId = 0,
                Id = 123,
                TakenBy = "TestTakenBy",
                TimeActionTaken = new DateTime(1981, 11, 30)
            };

            var result = new EmailLogDto(el);

            Assert.AreEqual(el.Action, result.Action);
            Assert.AreEqual(el.EmailId, result.EmailId);
            Assert.AreEqual(el.Id, result.Id);
            Assert.AreEqual(el.TakenBy, result.TakenBy);
            Assert.AreEqual(el.TimeActionTaken, result.TimeActionTaken);
        }

    }
}