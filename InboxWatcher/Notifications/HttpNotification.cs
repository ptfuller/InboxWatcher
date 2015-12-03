using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Enum;
using MailKit;
using MimeKit;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;
using InboxWatcher.Attributes;

namespace InboxWatcher
{
    [NotificationAttribute("HttpNotification")]
    public class HttpNotification : AbstractNotification
    {
        [XmlAttribute]
        public string Url { get; set; }

        [XmlAttribute]
        public string HttpMethod { get; set; }

        [XmlAttribute]
        public string ContentType { get; set; }

        protected List<KeyValuePair<string, string>> Kvp;

        public override bool Notify(IMessageSummary summary, NotificationType notificationType)
        {
            string response;

            if (HttpMethod == WebRequestMethods.Http.Post)
            {
                using (var client = new HttpClient())
                {
                   var summ = new Summary(summary);
                   var result = client.PostAsJsonAsync(Url, summ).Result;
                }
            }

            if (HttpMethod == WebRequestMethods.Http.Get)
            {
                using (var client = new WebClient())
                {
                    var data = MessageSummaryToListKeyValuePair.Convert(summary);
                    var ub = new UriBuilder(Url);
                    ub.Query = HttpUtility.UrlEncode(
                        string.Join("&", data.Select(x =>
                            string.Format("{0}={1}", x.Key, x.Value))));
                    try
                    {
                        response = client.DownloadString(ub.Uri);
                    }
                    catch (WebException ex)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }

        public override string GetConfigurationScript()
        {
            var selectedOption = "";

            if (!string.IsNullOrEmpty(HttpMethod))
            {
                selectedOption = "<option value=\"" + HttpMethod + "\" selected=\"selected\">" + HttpMethod + "</option>";
            }

            var script = "function SetupNotificationConfig() {" +
                         "$('#notificationFormArea').append('<div class=\"form\"><div class=\"form-group\">" +
                         "<label for=\"httpMethodSelect\">HTTP Method:</label><select class=\"form-control\" id=\"httpMethodSelect\" name=\"HttpMethod\" value=\"" + HttpMethod + "\">" +
                         "<option value=\"POST\">POST JSON</option><option value=\"GET\">GET Query String</option>" + selectedOption + "</select></div>" +
                         "<input type=\"hidden\" value=\"-1\" name=\"Id\" id=\"editNotificationId\"/>" +
                         "<div class=\"form-group\"><label for=\"urlInput\">URL:</label><input type=\"text\" class=\"form-control\" id=\"urlInput\" name=\"Url\" value=\"" + Url +"\"/>" +
                         "</div></div><div class=\"form-group\"><button class=\"btn btn-default\" id=\"textfilesubmit\">Submit</button></div>');}";

            return script;
        }
    }
}