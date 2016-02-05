using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace InboxWatcher.WebAPI.Controllers
{
    public class DashboardController : ApiController
    {
        [Route("dashboard", Name = "Dashboard")]
        [HttpGet, HttpPost, HttpPut, HttpDelete, HttpHead, HttpOptions, AcceptVerbs("PATCH")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var content = Path.Combine(InboxWatcher.ResourcePath, "dashboard.html");
            
            response.Content = new StringContent(File.ReadAllText(content));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            return response;
        }
    }
}