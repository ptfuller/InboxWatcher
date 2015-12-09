using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.DTO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using InboxWatcher.Interface;
using Ploeh.AutoFixture;

namespace InboxWatcher.DTO.Tests
{
    [TestClass()]
    public class EmailDtoTests
    {
        [TestMethod()]
        public void EmailDtoTest()
        {
            Mapper.CreateMap<Email, EmailDto>();
            Mapper.AssertConfigurationIsValid();

            var fixture = new Fixture();

            var email = fixture.Build<Email>()
                .Without(x => x.EmailLogs)
                .Without(x => x.ImapMailBoxConfiguration)
                .Create();

            var dto = new EmailDto(email);

            Assert.AreEqual(email.BodyText, dto.BodyText);
            Assert.AreEqual(email.EnvelopeID, dto.EnvelopeID);
            Assert.AreEqual(email.Id, dto.Id);
            Assert.AreEqual(email.ImapMailBoxConfigurationId, dto.ImapMailBoxConfigurationId);
            Assert.AreEqual(email.InQueue, dto.InQueue);
            Assert.AreEqual(email.MarkedAsRead, dto.MarkedAsRead);
            Assert.AreEqual(email.Minutes, dto.Minutes);
            Assert.AreEqual(email.Sender, dto.Sender);
            Assert.AreEqual(email.Subject, dto.Subject);
            Assert.AreEqual(email.TimeReceived, dto.TimeReceived);
        }

    }
}