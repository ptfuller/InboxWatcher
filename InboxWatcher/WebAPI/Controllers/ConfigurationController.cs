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
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;
using System.Xml.Serialization;
using InboxWatcher.Attributes;
using InboxWatcher.DTO;
using InboxWatcher.Properties;
using Newtonsoft.Json.Linq;
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
                var selection = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == id);
                if (selection != null) return selection;

                return new ImapMailBoxConfiguration();
            }
        }

        [Route("all")]
        public IEnumerable<IClientConfiguration> GetMailBoxConfigurations()
        {
            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.ImapMailBoxConfigurations.ToList();
                return selection;
            }
        }

        [Route("")]
        [HttpPost]
        public HttpResponseMessage Post(ClientConfigurationDto conf)
        {
            using (var ctx = new MailModelContainer())
            {
                //update
                if (ctx.ImapMailBoxConfigurations.Any(x => x.Id == conf.Id))
                {
                    var toReplace = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == conf.Id);

                    if (toReplace == null) return RedirectToUi(Request);

                    ctx.Entry(toReplace).CurrentValues.SetValues(conf.GetMailBoxConfiguration());
                }
                else //add new
                {
                    ctx.ImapMailBoxConfigurations.Add(conf.GetMailBoxConfiguration());
                }
                ctx.SaveChanges();
            }

            Task.Factory.StartNew(InboxWatcher.ConfigureMailBoxes);

            return RedirectToUi(Request);
        }

        [Route("mailbox/delete/{id:int}")]
        [HttpDelete]
        public HttpResponseMessage DeleteMailBox(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.ImapMailBoxConfigurations.First(x => x.Id == id);
                ctx.ImapMailBoxConfigurations.Attach(selection);
                ctx.ImapMailBoxConfigurations.Remove(selection);
                ctx.SaveChanges();
            }

            Task.Factory.StartNew(InboxWatcher.ConfigureMailBoxes);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [Route("notifications/{id:int}")]
        public IEnumerable<NotificationConfiguration> GetNotificationConfigurations(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedNotifications = ctx.NotificationConfigurations.Where(x => x.ImapMailBoxConfigurationId == id).ToList();
                selectedNotifications.ForEach(x => x.NotificationType = x.NotificationType.Split('.')[1]);
                return selectedNotifications;
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
            var types = FindNotificationConfigurationTypes();

            return types.Select(t => t.Name).ToList();
        }

        private IEnumerable<Type> FindNotificationConfigurationTypes()
        {
            var ass = Assembly.GetExecutingAssembly().GetTypes();
            var types = from t in ass
                        let attributes = t.GetCustomAttributes(typeof(NotificationAttribute), true)
                        where attributes != null && attributes.Length > 0
                        select new { Type = t, Attributes = attributes.Cast<NotificationAttribute>() };

            return types.Select(x => x.Type).ToList();
        }

        [Route("notifications/data/{type}")]
        public HttpResponseMessage GetNotificationScript(string type)
        {
            var notificationTypes = FindNotificationConfigurationTypes();

            var t = notificationTypes.First(x => x.Name.Equals(type));

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
            var notificationTypes = FindNotificationConfigurationTypes();

            //determine type of notification
            var t = notificationTypes.First(x => x.Name.Equals(data.First(y => y.Key.Equals("Type")).Value.ToString()));

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

            Task.Factory.StartNew(InboxWatcher.ConfigureMailBoxes);

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        private HttpResponseMessage RedirectToUi(HttpRequestMessage request)
        {
            var response = request.CreateResponse(HttpStatusCode.Redirect);
            var url = request.RequestUri.GetLeftPart(UriPartial.Authority);
            response.Headers.Location = new Uri(url + "/config/ui");

            return response;
        }
    }
}