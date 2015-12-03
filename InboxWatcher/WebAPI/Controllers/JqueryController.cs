using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using InboxWatcher.Properties;

namespace InboxWatcher.WebAPI.Controllers
{
    public class JqueryController : ApiController
    {
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Resources.jquery_2_1_4_min);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("css/dashboard.css")]
        public HttpResponseMessage GetCss()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Resources.dashboard1);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/css");

            return response;
        }
    }
}