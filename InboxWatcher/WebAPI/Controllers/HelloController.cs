using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using MailKit;

namespace InboxWatcher.WebAPI.Controllers
{
    public class HelloController : ApiController
    {
        public Envelope Post([FromBody] Envelope summary)
        {
            return summary;
        }

        public string Get()
        {
            var queryString = Request.GetQueryNameValuePairs();
            return string.Join("&",queryString.Select(x => string.Format("{0}={1}", x.Key, x.Value)));
        }
    }
}