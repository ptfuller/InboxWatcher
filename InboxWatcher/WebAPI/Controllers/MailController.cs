using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using MailKit;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InboxWatcher.WebAPI.Controllers
{
    public class MailController : ApiController
    {
        [Route("mailboxes")]
        [HttpGet]
        public IEnumerable<string> GetMailboxes()
        {
            return InboxWatcher.MailBoxes.Select(x => x.MailBoxName).ToList();
        }

        [Route("mailboxes/{mailBoxName}/messages")]
        [HttpGet]
        public IEnumerable<Summary> Get(string mailBoxName)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));
            return selectedMailBox?.EmailList.Select(messageSummary => new Summary(messageSummary)).ToList();
        }

        [Route("mailboxes/{mailBoxName}/messages/{uniqueId}")]
        [HttpGet]
        public Message Get(string mailBoxName, uint uniqueId)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));
            return new Message(selectedMailBox?.GetMessage(uniqueId));
        }

        [Route("mailboxes/{mailBoxName}/messages/{uniqueId}/sendto/{emailDestination}")]
        [HttpGet]
        public string Get(string mailBoxName, uint uniqueId, string emailDestination)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));

            if (selectedMailBox.SendMail(uniqueId, emailDestination))
            {
                return "Sending message " + uniqueId + " to " + emailDestination;
            }

            return "There was a problem sending message " + uniqueId + " to " + emailDestination;
        }

        //Get this email and send it.  Also move it to another folder.  Log that we've done this in the DB.
        //public string Get(string mailBoxName, uint uniqueId, string emailDestination, string moveToFolder)
        //{
            
        //}
    }
}