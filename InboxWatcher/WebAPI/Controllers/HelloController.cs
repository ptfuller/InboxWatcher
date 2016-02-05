using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;

namespace InboxWatcher.WebAPI.Controllers
{
    public class HelloController : ApiController
    {
        public ISummary Post(Summary summary)
        {
            return summary;
        }

        [Route("")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);

            var link = Url.Link("Dashboard", new {});

            response.Headers.Location = new Uri(link);

            return response;
        }
    }
}