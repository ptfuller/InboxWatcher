using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using InboxWatcher.Attributes;
using InboxWatcher.Interface;
using InboxWatcher.Notifications;

namespace InboxWatcher.WebAPI.Controllers
{
    [RoutePrefix("notifications")]
    public class NotificationController : ApiController
    {
        [Route("")]
        [HttpGet]
        public IEnumerable<NotificationConfiguration> GetNotificationConfigurations()
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedNotifications = ctx.NotificationConfigurations.ToList();
                selectedNotifications.ForEach(x => x.NotificationType = x.NotificationType.Split('.')[2]);
                return selectedNotifications;
            }
        }

        [Route("mailboxes/{mbname}")]
        [HttpGet]
        public IEnumerable<NotificationConfiguration> GetNotificationConfigurations(string mbname)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedNotifications = ctx.NotificationConfigurations.Where(x => x.ImapMailBoxConfiguration.MailBoxName.Equals(mbname)).ToList();
                selectedNotifications.ForEach(x => x.NotificationType = x.NotificationType.Split('.')[2]);
                return selectedNotifications;
            }
        }

        [Route("{id:int}")]
        [HttpGet]
        public HttpResponseMessage GetSingleNotificationScript(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                if (!ctx.NotificationConfigurations.Any()) return new HttpResponseMessage(HttpStatusCode.NotFound);

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

        [Route("{id:int}")]
        [HttpDelete]
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

        [Route("types")]
        [HttpGet]
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

        [HttpGet]
        [Route("data/{type}")]
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
        [Route("test/{id:int}")]
        public HttpResponseMessage TestNotification(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedConfig = ctx.NotificationConfigurations.First(x => x.Id == id);

                var t = Type.GetType(selectedConfig.NotificationType);
                if (t == null) return new HttpResponseMessage(HttpStatusCode.NotFound);

                var action = (INotificationAction)Activator.CreateInstance(t);
                action = action.DeSerialize(selectedConfig.ConfigurationXml);
                action.TestNotification();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private AbstractNotification GetNotificationInstance(Dictionary<string, object> data)
        {
            var notificationTypes = FindNotificationConfigurationTypes();

            //determine type of notification
            var t = notificationTypes.First(x => x.Name.Equals(data.First(y => y.Key.Equals("Type")).Value.ToString()));

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

            return notificationAction;
        }

        [HttpPut]
        [Route("")]
        public NotificationConfiguration UpdateNotificationConfiguration(Dictionary<string, object> data)
        {
            var notificationAction = GetNotificationInstance(data);
            var id = int.Parse(data.First(x => x.Key.Equals("Id")).Value.ToString());

            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.NotificationConfigurations.Find(id);
                selection.ConfigurationXml = notificationAction.Serialize();
                selection.ImapMailBoxConfigurationId = int.Parse(data.First(x => x.Key.Equals("MailBoxId")).Value.ToString());

                ctx.Entry(selection).State = EntityState.Modified;
                ctx.SaveChanges();
                return selection;
            }
        }


        [HttpPost]
        [Route("")]
        public NotificationConfiguration PostNotification(Dictionary<string, object> data)
        {
            var notificationAction = GetNotificationInstance(data);

            var not = new NotificationConfiguration()
            {
                ConfigurationXml = notificationAction.Serialize(),
                NotificationType = notificationAction.GetType().FullName,
                ImapMailBoxConfigurationId =
                            int.Parse(data.First(x => x.Key.Equals("MailBoxId")).Value.ToString()),
            };

            //add a new record
            using (var ctx = new MailModelContainer())
            {
                ctx.NotificationConfigurations.Add(not);
                ctx.SaveChanges();
                var config = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == not.ImapMailBoxConfigurationId);
                Task.Factory.StartNew(() => InboxWatcher.ConfigureMailBox(config));
            }

            return not;
        }
    }
}