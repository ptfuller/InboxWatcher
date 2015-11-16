using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using InboxWatcher.Notifications;
using InboxWatcher.Properties;
using RazorEngine;
using RazorEngine.Templating;

namespace InboxWatcher.WebAPI.Controllers
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
        public IEnumerable<ImapMailBoxConfiguration> GetMailBoxConfigurations()
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.ToList();
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
    }
}