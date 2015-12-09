using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using InboxWatcher.DTO;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using MailKit;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InboxWatcher.WebAPI.Controllers
{
    public class MailController : ApiController
    {
        [Route("mailboxes/{mailBoxName}/emails/{fromtoday:bool?}")]
        [HttpGet]
        public IEnumerable<IEmail> GetEmails(string mailBoxName, bool fromtoday = false)
        {
            using (var ctx = new MailModelContainer())
            {
                var emails = ctx.Emails
                    .Where(x => x.ImapMailBoxConfigurationId == ctx.ImapMailBoxConfigurations.FirstOrDefault(y => y.MailBoxName.Equals(mailBoxName)).Id)
                    .Include(l => l.EmailLogs)
                    .Take(500);

                if (fromtoday)
                    emails =
                        emails.Where(
                            x =>
                                x.TimeReceived.Year == DateTime.Now.Year && 
                                x.TimeReceived.Month == DateTime.Now.Month &&
                                x.TimeReceived.Day == DateTime.Now.Day);

                var emailDtos = new List<IEmail>();

                foreach (var email in emails)
                {
                    emailDtos.Add(new EmailDto(email));
                }

                return emailDtos.OrderByDescending(x => x.Id);
            }
        }

        [Route("mailboxes/{mailBoxName}/folders")]
        [HttpGet]
        public IEnumerable<string> GetFolders(string mailBoxName)
        {
            var mailbox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));
            return mailbox?.EmailFolders.Select(x => x.FullName);
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
            var selectedMailBox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));
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


        [Route("mailboxes/{mailBoxName}/status")]
        [HttpGet]
        public MailBoxStatusDto GetStatus(string mailBoxName)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));

            return selectedMailBox.Status();
        }

        //Get this email and send it.  Also move it to another folder.  Log that we've done this in the DB.
        //public string Get(string mailBoxName, uint uniqueId, string emailDestination, string moveToFolder)
        //{

        //}
    }
}