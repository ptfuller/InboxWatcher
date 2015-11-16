using System;
using System.IO;
using System.Text;
using InboxWatcher.Enum;
using MailKit;
using Org.BouncyCastle.Asn1.X509;

namespace InboxWatcher.Notifications
{
    public class TextFileNotification : INotificationAction
    {
        private string _filePath;

        public TextFileNotification(string filePath)
        {
            _filePath = filePath;
        }

        public bool Notify(IMessageSummary summary, NotificationType notificationType)
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
                File.AppendAllText(_filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}