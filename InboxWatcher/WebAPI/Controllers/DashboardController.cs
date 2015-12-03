using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using InboxWatcher.Properties;
using RazorEngine;
using RazorEngine.Templating;

namespace InboxWatcher.WebAPI.Controllers
{
    public class DashboardController : ApiController
    {
        [Route("dashboard", Name = "Dashboard")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var model = new HttpNotification();

            var parsedView = Engine.Razor.RunCompile(
                Resources.dashboard,
                "templateKey",
                modelType: typeof(HttpNotification),
                model: model);

            response.Content = new StringContent(parsedView);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            return response;
        }
    }
}