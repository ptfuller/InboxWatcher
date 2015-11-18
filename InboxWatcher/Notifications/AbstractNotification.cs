using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Policy;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Enum;
using MailKit;
using MimeKit;
using Org.BouncyCastle.Apache.Bzip2;

namespace InboxWatcher
{
    public abstract class AbstractNotification
    {
        [XmlAttribute]
        public virtual string Id { get; set; }

        [XmlAttribute]
        public virtual string Type { get; set; }

        public virtual bool Notify(IMessageSummary summary, NotificationType notificationType)
        {
            return false;
        }

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

        public virtual string GetConfigurationScript()
        {
            return "function SetupNotificationConfig() {$('#notificationFormArea').html('<p>No configuration script supplied</p>');}";
        }
    }
}