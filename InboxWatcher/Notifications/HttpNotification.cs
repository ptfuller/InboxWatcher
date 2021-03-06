﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using InboxWatcher.Attributes;
using InboxWatcher.Enum;
using InboxWatcher.ImapClient;
using MailKit;

namespace InboxWatcher.Notifications
{
    [Notification("HttpNotification")]
    public class HttpNotification : AbstractNotification
    {
        [XmlAttribute]
        public string Url { get; set; }

        [XmlAttribute]
        public string HttpMethod { get; set; }

        [XmlAttribute]
        public string ContentType { get; set; }

        protected List<KeyValuePair<string, string>> Kvp;

        public override async Task<bool> Notify(IMessageSummary summary, NotificationType notificationType, string mailBoxName)
        {
            string response;

            if (HttpMethod == WebRequestMethods.Http.Post)
            {
                using (var client = new HttpClient())
                {
                    var summ = new NotificationSummary(summary, notificationType) {MailBoxName = mailBoxName};
                    try
                    {
                        var result = await client.PostAsJsonAsync(Url, summ, Util.GetCancellationToken(10000));
                    }
                    catch (Exception)
                    {
                        
                    }
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