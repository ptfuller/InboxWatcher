﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using MailKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            var link = Url.Link("ui", new {});

            response.Headers.Location = new Uri(link);

            return response;
        }
    }
}