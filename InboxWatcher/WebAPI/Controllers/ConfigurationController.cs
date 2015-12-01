using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.DTO;
using InboxWatcher.Properties;
using RazorEngine;
using RazorEngine.Templating;

namespace InboxWatcher
{
    [RoutePrefix("config")]
    public class ConfigurationController : ApiController
    {
        [Route("ui")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var model = new HttpNotification();

            var parsedView = Engine.Razor.RunCompile(
                Resources.configuration,
                "templateKey", 
                modelType: typeof(HttpNotification),
                model: model);

            response.Content = new StringContent(parsedView);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            
            return response;
        }

        [Route("{id:int}")]
        public ImapMailBoxConfiguration Get(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.First(x => x.Id == id);
            }
        }

        [Route("all")]
        public IEnumerable<IClientConfiguration> GetMailBoxConfigurations()
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.ToList();
                //return Enumerable.Cast<IClientConfiguration>(ctx.ImapMailBoxConfigurations.Select(config => new ClientConfigurationDto()
                //{
                //    HostName = config.HostName,
                //    Id = config.Id,
                //    MailBoxName = config.MailBoxName,
                //    Password = config.Password,
                //    Port = config.Port,
                //    UserName = config.UserName,
                //    UseSecure = config.UseSecure
                //})).ToList();
            }
        }

        [Route("")]
        public string Post(ImapMailBoxConfiguration conf)
        {
            using (var ctx = new MailModelContainer())
            {
                ctx.ImapMailBoxConfigurations.Add(conf);
                ctx.SaveChanges();
            }

            return "great success!";
        }

        [Route("notifications/{id:int}")]
        public IEnumerable<NotificationConfiguration> GetNotificationConfigurations(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.NotificationConfigurations.Where(x => x.ImapMailBoxConfigurationId == id).ToList();
            }
        }

        [Route("notifications/id/{id:int}")]
        public HttpResponseMessage GetSingleNotificationScript(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var notification = ctx.NotificationConfigurations.First(x => x.Id == id);

                var t = Type.GetType(notification.NotificationType);

                if (t == null) return new HttpResponseMessage(HttpStatusCode.NotFound);

                var action = (AbstractNotification)Activator.CreateInstance(t);
                var serializedNotification = action.DeSerialize(notification.ConfigurationXml);

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(serializedNotification.GetConfigurationScript());
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

                return response;
            }
        }

        [Route("notifications/id/{id:int}")]
        public HttpResponseMessage Delete(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var itemToDelete = ctx.NotificationConfigurations.Single(x => x.Id == id);
                ctx.NotificationConfigurations.Remove(itemToDelete);
                ctx.SaveChanges();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [Route("notifications/types")]
        public IEnumerable<string> GetNotificationConfigurationTypes()
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.NotificationConfigurations.Select(x => x.NotificationType).Distinct().ToList();
            }
        }

        [Route("notifications/data/{type}")]
        public HttpResponseMessage GetNotificationScript(string type)
        {
            var t = Type.GetType(type);

            if (t == null) return new HttpResponseMessage(HttpStatusCode.NotFound);

            var action = (AbstractNotification)Activator.CreateInstance(t);

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(action.GetConfigurationScript());

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [HttpPost]
        [Route("notifications/id/test/{id:int}")]
        public HttpResponseMessage TestNotification(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedConfig = ctx.NotificationConfigurations.First(x => x.Id == id);

                var t = Type.GetType(selectedConfig.NotificationType);
                if (t == null) return new HttpResponseMessage(HttpStatusCode.NotFound);

                var action = (AbstractNotification) Activator.CreateInstance(t);
                action = action.DeSerialize(selectedConfig.ConfigurationXml);
                action.TestNotification();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        [Route("notifications")]
        public HttpResponseMessage PostNotification(Dictionary<string, object> data)
        {
            //determine type of notification
            string notificationType = data.First(x => x.Key.Equals("Type")).Value.ToString();
            var t = Type.GetType(notificationType);

            //get the notification's ID.  If -1 it's a new notification otherwise we are editing an existing notification
            var id = int.Parse(data.First(x => x.Key.Equals("Id")).Value.ToString());

            //create an instance of the notification type
            var notificationAction = Activator.CreateInstance(t) as AbstractNotification;

            //Add all of the properties to the notification object
            foreach (var prop in data.Where(prop => !string.IsNullOrEmpty(prop.Key) && prop.Value != null))
            {
                //if the type doesn't have one of the properties passed in data then we ignore it
                try
                {
                    t.GetProperty(prop.Key).SetValue(notificationAction, prop.Value);
                }
                catch (NullReferenceException ex)
                {
                    
                }
            }

            //update or add a new record
            using (var ctx = new MailModelContainer())
            {
                //this is a new one
                if (id == -1)
                {
                    var not = new NotificationConfiguration()
                    {
                        ConfigurationXml = notificationAction.Serialize(),
                        NotificationType = t.FullName,
                        ImapMailBoxConfigurationId =
                            int.Parse(data.First(x => x.Key.Equals("MailBoxId")).Value.ToString()),
                    };

                    ctx.NotificationConfigurations.Add(not);
                    ctx.SaveChanges();
                }
                else //update existing
                {
                    var selectedNotification = ctx.NotificationConfigurations.First(x => x.Id == id);
                    selectedNotification.ConfigurationXml = notificationAction.Serialize();
                    selectedNotification.ImapMailBoxConfigurationId =
                        int.Parse(data.First(x => x.Key.Equals("MailBoxId")).Value.ToString());
                    
                    ctx.Entry(selectedNotification).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}