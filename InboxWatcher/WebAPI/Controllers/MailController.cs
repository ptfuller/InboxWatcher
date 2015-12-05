﻿using System.Collections.Generic;
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
        [Route("mailboxes/{mailBoxName}/emails")]
        [HttpGet]
        public IEnumerable<Email> GetEmails(string mailBoxName)
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.First(x => x.MailBoxName.Equals(mailBoxName)).Emails.ToList();
            }
        }

        [Route("mailboxes")]
        [HttpGet]
        public IEnumerable<string> GetMailboxes()
        {
            return InboxWatcher.MailBoxes != null && InboxWatcher.MailBoxes.Count > 0 ? InboxWatcher.MailBoxes.Select(x => x.MailBoxName).ToList() : new List<string>();
        }

        [Route("mailboxes/{mailBoxName}")]
        [HttpGet]
        public IEnumerable<Summary> Get(string mailBoxName)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));
            return selectedMailBox?.EmailList.Select(messageSummary => new Summary(messageSummary)).ToList();
        }

        [Route("mailboxes/{mailBoxName}/{uniqueId}")]
        [HttpGet]
        public Message Get(string mailBoxName, uint uniqueId)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));
            return new Message(selectedMailBox?.GetMessage(uniqueId));
        }

        [Route("mailboxes/{mailBoxName}/{uniqueId}/sendto/{emailDestination}")]
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