using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Enum;
using MailKit;
using MimeKit;

namespace InboxWatcher.Notifications
{
    public abstract class AbstractNotification
    {
        [XmlAttribute]
        public virtual string Id { get; set; }

        [XmlAttribute]
        public virtual string Type { get; set; }

        public abstract bool Notify(IMessageSummary summary, NotificationType notificationType, string mailBoxName);

        public virtual string Serialize()
        {
            var xmlserializer = new XmlSerializer(GetType());
            var stringWriter = new StringWriter();
            using (var writer = XmlWriter.Create(stringWriter))
            {
                xmlserializer.Serialize(writer, this);
                return stringWriter.ToString();
            }
        }

        public virtual AbstractNotification DeSerialize(string xmlString)
        {
            var serilaizer = new XmlSerializer(GetType());
            var sr = new StringReader(xmlString);
            var xmlReader = XmlReader.Create(sr);

            return Convert.ChangeType(serilaizer.Deserialize(xmlReader), GetType()) as AbstractNotification;
        }

        public abstract string GetConfigurationScript();

        public void TestNotification()
        {
            var testSummary = new MessageSummary(0);
            var env = new Envelope();
            env.Subject = "testNotificationSubject";
            env.From.Add(new MailboxAddress("testNotificationName", "TestNotificationAddress"));
            env.MessageId = "123TestAbc";
            env.Date = DateTime.Now;
            testSummary.Envelope = env;

            Notify(testSummary, NotificationType.Received, "testNotification");
        }
    }
}