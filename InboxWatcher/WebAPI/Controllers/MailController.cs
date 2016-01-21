using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
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

                if (fromtoday)
                    emails =
                        emails.Where(
                            x =>
                                x.TimeReceived.Year == DateTime.Now.Year && 
                                x.TimeReceived.Month == DateTime.Now.Month &&
                                x.TimeReceived.Day == DateTime.Now.Day);

                var emailDtos = new List<IEmail>();

                foreach (var email in emails.Take(500))
                {
                    emailDtos.Add(new EmailDto(email));
                }

                return emailDtos.OrderByDescending(x => x.Id);
            }
        }

        [Route("mailboxes/{mailBoxName}/emails/count")]
        [HttpGet]
        public int GetCountEmails(string mailBoxName)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedMailBox =
                    ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));

                if (selectedMailBox == null) return 0;

                return ctx.Emails.Count(x => x.ImapMailBoxConfigurationId == selectedMailBox.Id);
            }
        }

        [Route("mailboxes/{mailBoxName}/search")]
        [HttpGet]
        public PagedResult Search(string mailBoxName, string search = "", string order = "asc", int limit = 0, int offset = 0, bool inQueue = false)
        {
            if (string.IsNullOrEmpty(search))
            {
                search = "";
            }

            search = search.ToLower();

            var results = new PagedResult();

            using (var ctx = new MailModelContainer())
            {
                var rows = ctx.Emails.Where(x => x.ImapMailBoxConfiguration.MailBoxName.Equals(mailBoxName)).Include(em => em.EmailLogs);

                if (!rows.Any()) return results;

                if (!string.IsNullOrEmpty(search))
                {
                    rows = rows.Where(x => x.Sender.ToLower().Contains(search) || 
                        x.Subject.ToLower().Contains(search) || 
                        x.BodyText.ToLower().Contains(search) || 
                        x.EmailLogs.Any(log => log.TakenBy.Contains(search) || log.Action.Contains(search)));
                }

                if (inQueue)
                {
                    rows = rows.Where(x => x.InQueue);
                }

                rows = !order.Equals("asc") ? rows.OrderByDescending(x => x.TimeReceived) : rows.OrderBy(x => x.TimeReceived);

                var rowCount = rows.Count();

                rows = limit != 0 ? rows.Skip(offset).Take(limit) : rows;

                results.rows = rows.ToList().Select(email => new EmailDto(email));
                results.total = rowCount;

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
        public async Task<Message> Get(string mailBoxName, uint uniqueId)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));

            var message = selectedMailBox?.GetMessage(uniqueId);

            return message == null ? null : new Message(await message);
        }

        [Route("mailboxes/{mailBoxName}/{uniqueId}/sendto/{emailDestination}/{moveToDestinationFolder}")]
        [HttpGet]
        public async Task<HttpResponseMessage> Get(string mailBoxName, uint uniqueId, string emailDestination, bool moveToDestinationFolder = false)
        {
            Trace.WriteLine($"{emailDestination} is trying to get message {uniqueId}");

            var selectedMailBox = InboxWatcher.MailBoxes.First(x => x.MailBoxName.Equals(mailBoxName));

            var selectedMessage = await selectedMailBox.GetMessage(uniqueId);

            if (await selectedMailBox.SendMail(selectedMessage, uniqueId, emailDestination, moveToDestinationFolder))
            {
                Trace.WriteLine($"{emailDestination} got message with subject: {selectedMessage.Subject}");
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
        

        [Route("mailboxes/{mailBoxName}/{uniqueId}/movemailboxes/{destination}/sendto/{address}/actionby/{username}")]
        [HttpPut]
        public async Task<HttpResponseMessage> MoveMessageToAlternateMailbox(string mailBoxName, uint UniqueId, string destination, string address, string username)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));

            if (selectedMailBox == null) { return new HttpResponseMessage(HttpStatusCode.InternalServerError);}

            var selectedMessage = await selectedMailBox.GetMessage(UniqueId);

            if (await selectedMailBox.SendMail(selectedMessage, UniqueId, address, false))
            {
                await selectedMailBox.MoveMessage(UniqueId, selectedMessage.MessageId, destination, username);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        
        [Route("mailboxes/{mailBoxName}/{uniqueId}/movetofolder/{destination}/actionby/{username}")]
        [HttpPut]
        public async Task<HttpResponseMessage> MoveMessageToFolder(string mailBoxName, uint UniqueId, string destination, string username)
        {
            var selectedMailBox = InboxWatcher.MailBoxes.FirstOrDefault(x => x.MailBoxName.Equals(mailBoxName));

            if (selectedMailBox == null) { return new HttpResponseMessage(HttpStatusCode.InternalServerError); }

            var messageSummary = selectedMailBox.EmailList.FirstOrDefault(x => x.UniqueId.Id == UniqueId);

            if (messageSummary == null) { return new HttpResponseMessage(HttpStatusCode.InternalServerError); }

            await selectedMailBox.MoveMessage(messageSummary, destination, username);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}