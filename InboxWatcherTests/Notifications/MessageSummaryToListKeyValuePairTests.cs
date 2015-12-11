using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.Notifications;
using InboxWatcher.ImapClient;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Ploeh.AutoFixture;

namespace InboxWatcher.Notifications.Tests
{
    [TestClass()]
    public class MessageSummaryToListKeyValuePairTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            fix = new Fixture();
            env = fix.Create<Envelope>();

            ms = new MessageSummary(0)
            {
                Envelope = env
            };
        }

        private MessageSummary ms;
        private Envelope env;
        private Fixture fix;

        [TestMethod()]
        public void ConvertTest()
        {
            var result = MessageSummaryToListKeyValuePair.Convert(ms);

            Assert.AreEqual(env.MessageId, result.First(x => x.Key.Equals("EnvelopeID")).Value);
        }
    }
}