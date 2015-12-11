using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace InboxWatcher.WebAPI.Controllers
{
    [RoutePrefix("documentation")]
    public class HelpController : ApiController
    {
        [Route("")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var content = Path.Combine(InboxWatcher.ResourcePath, "help.html");

            response.Content = new StringContent(File.ReadAllText(content));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            return response;
        }
    }
}