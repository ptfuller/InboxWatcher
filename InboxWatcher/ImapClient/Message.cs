using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlchemyInboxWatcher;
using MimeKit;
using MimeKit.Text;

namespace InboxWatcher
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
            MessageText = inMessage.GetTextBody(TextFormat.Text);
            if (string.IsNullOrEmpty(MessageText))
            {
                MessageText = HtmlToText.ConvertHtml(inMessage.HtmlBody).Replace("\r", "").Replace("\n","");
            }
            Bcc = inMessage.Bcc.InternetAddressListToDictionary();
            Cc = inMessage.Cc.InternetAddressListToDictionary();
            Date = inMessage.Date;
            From = inMessage.From.InternetAddressListToDictionary();
            Subject = inMessage.Subject;
            MessageId = inMessage.MessageId;
        }
    }
}
