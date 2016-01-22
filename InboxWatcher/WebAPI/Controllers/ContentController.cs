using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using InboxWatcher.Properties;

namespace InboxWatcher.WebAPI.Controllers
{
    public class ContentController : ApiController
    {
        [Route("js/jquery.js")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "jquery.min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/underscore.js")]
        public HttpResponseMessage GetUnderscore()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "underscore-min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/epoxy.js")]
        public HttpResponseMessage GetEpoxy()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "backbone.epoxy.min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/backbone.js")]
        public HttpResponseMessage GetBackBone()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "backbone-min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("css/dashboard.css")]
        public HttpResponseMessage GetCss()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "dashboard.css");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/css");

            return response;
        }

        [Route("css/bootstrap.css")]
        public HttpResponseMessage GetBootstrapCss()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "bootstrap.css");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/css");

            return response;
        }

        [Route("js/jquery.signalR-2.2.0.js")]
        public HttpResponseMessage GetSignalR()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "jquery.signalR-2.2.0.min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/bootstrap.js")]
        public HttpResponseMessage GetBootstrap()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = Path.Combine(InboxWatcher.ResourcePath, "bootstrap.min.js");
            response.Content = new StringContent(File.ReadAllText(content));

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }
    }
}