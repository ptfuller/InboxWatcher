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
            response.Content = new StringContent(Resources.jquery_2_1_4_min);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/underscore.js")]
        public HttpResponseMessage GetUnderscore()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Resources.underscore);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/epoxy.js")]
        public HttpResponseMessage GetEpoxy()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Resources.epoxy);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");

            return response;
        }

        [Route("js/backbone.js")]
        public HttpResponseMessage GetBackBone()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Resources.backbone);

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