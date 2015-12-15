using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using WebGrease.Css.Extensions;

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
                    .Include(l => l.EmailLogs);
                    //.Take(500);

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

        [Route("mailboxes/{mailBoxName}/search")]
        [HttpGet]
        public PagedResult Search(string mailBoxName, string search = "", string order = "asc", int limit = 0, int offset = 0)
        {
            search = search.ToLower();

            var results = new PagedResult();

            using (var ctx = new MailModelContainer())
            {
                var mailbox = ctx.ImapMailBoxConfigurations.Include(x => x.Emails.Select(e => e.EmailLogs)).FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));

                if (mailbox == null) return results;

                IEnumerable<Email> rows = mailbox.Emails;

                if (!string.IsNullOrEmpty(search))
                {
                    rows = rows.Where(x => x.Sender.ToLower().Contains(search) || 
                        x.Subject.ToLower().Contains(search) || 
                        x.BodyText.ToLower().Contains(search) || 
                        x.TimeReceived.ToString("d").Contains(search));
                }

                if (!order.Equals("asc")) rows = rows.OrderByDescending(x => x.Id);

                rows = limit != 0 ? rows.Skip(offset).Take(limit) : rows;

                results.rows = rows.Select(email => new EmailDto(email)).ToList();
                results.total = mailbox.Emails.Count;

                return results;
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
        public IEnumerable<ISummary> Get(string mailBoxName)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));
            return selectedMailBox?.EmailList.Select(messageSummary => new Summary(messageSummary)).ToList().OrderByDescending(x => x.Received);
        }

        [Route("mailboxes/{mailBoxName}/{uniqueId}")]
        [HttpGet]
        public Message Get(string mailBoxName, uint uniqueId)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));
            return new Message(selectedMailBox?.GetMessage(uniqueId));
        }

        [Route("mailboxes/{mailBoxName}/{uniqueId}/sendto/{emailDestination}/{moveToDestinationFolder}")]
        [HttpGet]
        public HttpResponseMessage Get(string mailBoxName, uint uniqueId, string emailDestination, bool moveToDestinationFolder = false)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));

            var selectedMessage = selectedMailBox.GetMessage(uniqueId);

            if (selectedMailBox.SendMail(selectedMessage, uniqueId, emailDestination, moveToDestinationFolder))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }


        [Route("mailboxes/{mailBoxName}/status")]
        [HttpGet]
        public MailBoxStatusDto GetStatus(string mailBoxName)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));

            return selectedMailBox?.Status();
        }

    }
}