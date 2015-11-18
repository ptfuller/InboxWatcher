using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Enum;
using MailKit;

namespace InboxWatcher
{
    public class HttpNotification : AbstractNotification
    {
        [XmlAttribute]
        public string Url { get; set; }

        [XmlAttribute]
        public string HttpMethod { get; set; }

        [XmlAttribute]
        public string ContentType { get; set; }

        [XmlAttribute]
        public NotificationType NotificationType { get; set; }

        protected List<KeyValuePair<string, string>> Kvp;

        public override bool Notify(IMessageSummary summary, NotificationType notificationType)
        {
            NotificationType = notificationType;

            var client = new HttpClient();
            
            var content = new FormUrlEncodedContent(MessageSummaryToListKeyValuePair.Convert(summary));

            var response = client.PostAsync(Url, content).Result;

            return response.IsSuccessStatusCode;
        }

        public override AbstractNotification DeSerialize(string xmlString)
        {
            var serilaizer = new XmlSerializer(GetType());
            var sr = new StringReader(xmlString);
            var xmlReader = XmlReader.Create(sr);

            var temp = (HttpNotification) serilaizer.Deserialize(xmlReader);
            Url = temp.Url;
            HttpMethod = temp.HttpMethod;
            ContentType = temp.ContentType;
            NotificationType = temp.NotificationType;

            return this;
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