using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using InboxWatcher.DTO;

namespace InboxWatcher.WebAPI.Controllers
{
    [RoutePrefix("stats")]
    public class StatsController : ApiController
    {
        [Route("mailboxes/{mbname}")]
        [HttpGet]
        public object Get(string mbname)
        {
            var date = DateTime.Today;

            using (var ctx = new MailModelContainer())
            {
                var query = (from email in ctx.Emails
                    where email.ImapMailBoxConfiguration.MailBoxName.Equals(mbname)
                        && email.TimeReceived.Year == date.Year
                        && email.TimeReceived.Month == date.Month
                        && email.TimeReceived.Day == date.Day
                    group email by email.TimeReceived.Hour
                    into emailGroup
                    select emailGroup);

                var dict = new Dictionary<int, int[]>();

                foreach (var value in query)
                {
                    dict.Add(value.Key, new [] { value.Count(), (int) value.Average(x => x.Minutes) } );
                }

                return dict;
            }
        }

        [Route("mailboxes/{mbname}/activity")]
        [HttpGet]
        public object Activity(string mbname)
        {
            var date = DateTime.Today;

            using (var ctx = new MailModelContainer())
            {
                ctx.Configuration.ProxyCreationEnabled = false;

                var query = ctx.EmailLogs.Where(x => x.Email.TimeReceived.Year == date.Year
                                                     && x.Email.TimeReceived.Month == date.Month
                                                     && x.Email.TimeReceived.Day == date.Day
                                                     && x.Email.ImapMailBoxConfiguration.MailBoxName.Equals(mbname)
                                                     && x.Action.Contains("Sent")
                                                     && x.TakenBy.Contains("@"))
                                                     .GroupBy(y => y.TakenBy).Select(z => new {SupportRep = z.Key, EmailsTaken = z.Count()});

                

                return query.ToList();
            }
        }


        [Route("mailboxes/{mbname}/subjects")]
        [HttpGet]
        public object ActivitySubjects(string mbname)
        {
            var date = DateTime.Today;

            using (var ctx = new MailModelContainer())
            {
                ctx.Configuration.ProxyCreationEnabled = false;

                var query = ctx.Emails.Where(
                    x =>
                        x.ImapMailBoxConfiguration.MailBoxName.Equals(mbname) &&
                        x.EmailLogs.Any(log => log.TakenBy.Contains("@") && log.Action.Contains("Sent to") && log.Action.Contains("and moved to")) &&
                        x.InQueue == false &&
                        x.TimeReceived.Year == date.Year &&
                        x.TimeReceived.Month == date.Month &&
                        x.TimeReceived.Day == date.Day)
                    .Select(x => new {Name = x.EmailLogs.FirstOrDefault(l => l.TakenBy.Contains("@")).TakenBy, Subject = x.Subject})
                    .GroupBy(x => x.Name, y => y.Subject).ToDictionary(x => x.Key);


                //todo not working yet
                return query;
            }
        }
    }
}