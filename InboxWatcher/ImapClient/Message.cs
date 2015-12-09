using System;
using System.Collections.Generic;
using InboxWatcher.Interface;
using MimeKit;
using MimeKit.Text;

namespace InboxWatcher.ImapClient
{
    public class Message : IMessage
    {
        public Dictionary<string, string> Bcc { get; set; }
        public Dictionary<string, string> Cc { get; set; }
        public string MessageText { get; set; }
        public DateTimeOffset Date { get; set; }
        public Dictionary<string, string> From { get; set; }
        public string MessageId { get; set; }
        public string Subject { get; set; }


        public Message(MimeMessage inMessage)
        {
            MessageId = inMessage.MessageId;
            From = inMessage.From.InternetAddressListToDictionary();
            Cc = inMessage.Cc.InternetAddressListToDictionary();
            Bcc = inMessage.Bcc.InternetAddressListToDictionary();
            Date = inMessage.Date;
            Subject = inMessage.Subject;
            MessageText = inMessage.GetTextBody(TextFormat.Text);
            if (string.IsNullOrEmpty(MessageText))
            {
                MessageText = HtmlToText.ConvertHtml(inMessage.HtmlBody).Replace("\r", "").Replace("\n", "");
            }
        }
    }
}
