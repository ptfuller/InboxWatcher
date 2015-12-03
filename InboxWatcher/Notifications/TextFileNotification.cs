using System;
using System.IO;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Attributes;
using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher
{
    [NotificationAttribute("TextFileNotification")]
    public class TextFileNotification : AbstractNotification
    {
        [XmlAttribute]
        public string FilePath { get; set; } = "";

        public override bool Notify(IMessageSummary summary, NotificationType notificationType)
        {
            var sb = new StringBuilder();
            sb.AppendLine("***** " + DateTime.Now + " : Action Happened *****");
            sb.AppendLine("Notification Type: " + notificationType);
            sb.AppendLine("Subject: " + summary.Envelope.Subject);
            sb.AppendLine("Time: " + summary.Envelope.Date);
            sb.AppendLine("Sender: " + summary.Envelope.From[0]);
            sb.AppendLine(summary.Date.GetType().Name + " : " + summary.Date);
            sb.AppendLine("---");

            try
            {
                File.AppendAllText(FilePath, sb.ToString());
            }

            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public override string GetConfigurationScript()
        {
            var script = "function SetupNotificationConfig() {" +
                         "$('#notificationFormArea').append('<p>Text File Notification:</p>');" +
                         "$('#notificationFormArea').append('<div class=\"form-group\"><label for=\"textfilepath\">Text File Path</label>" +
                         "<input type=\"text\" class=\"form-control\" id=\"textfilepath\" name=\"FilePath\" value=\"" + FilePath.Replace(@"\", @"\\") + "\"/></div>');" +
                         "$('#notificationFormArea').append('<input type=\"hidden\" value=\"-1\" name=\"Id\" id=\"editNotificationId\"/>');" +
                         "$('#notificationFormArea').append('<div class=\"form-group\"><button class=\"btn btn-default\" id=\"textfilesubmit\">Submit</button></div>');}";

            return script;
        }
    }
}