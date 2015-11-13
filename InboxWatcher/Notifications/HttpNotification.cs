using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Text;
using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher.Notifications
{
    public class HttpNotification : INotificationAction
    {
        protected string Url;
        protected string HttpMethod;
        protected string ContentType;
        protected NotificationType NotificationType;
        protected List<KeyValuePair<string, string>> Kvp;

        public virtual bool Notify(IMessageSummary summary, NotificationType notificationType)
        {
            NotificationType = notificationType;

            var client = new HttpClient();
            
            var content = new FormUrlEncodedContent(MessageSummaryToListKeyValuePair.Convert(summary));

            var response = client.PostAsync(Url, content).Result;

            return response.IsSuccessStatusCode;
        }

        public HttpNotification WithUrl(string url)
        {
            Url = url;
            return this;
        }

        public HttpNotification WithPostData(List<KeyValuePair<string,string>> kvp)
        {
            Kvp = kvp;
            return this;
        }

        public HttpNotification WithMethod(string method)
        {
            HttpMethod = method.ToUpper();
            return this;
        }

        public HttpNotification WithContentType(string contentType)
        {
            ContentType = contentType;
            return this;
        }

        protected string GetRequestMethod(string m)
        {
            if (m.Equals(WebRequestMethods.Http.Post))
            {
                return WebRequestMethods.Http.Post;
            }

            if (m.Equals(WebRequestMethods.Http.Get))
            {
                return WebRequestMethods.Http.Get;
            }

            return null;
        }
    }
}