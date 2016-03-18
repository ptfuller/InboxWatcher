using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using InboxWatcher.DTO;
using InboxWatcher.Interface;

namespace InboxWatcher.WebAPI.Controllers
{
    [RoutePrefix("filters")]
    public class FilterController : ApiController
    {
        [Route("")]
        [HttpGet]
        public IEnumerable<EmailFilter> GetEmailFilters()
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.EmailFilters.ToList();
            }
        }

        [Route("mailboxes/{mbname}")]
        [HttpGet]
        public IEnumerable<EmailFilter> GetMailBoxEmailFilters(string mbname)
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.EmailFilters.Where(x => x.ImapMailBoxConfiguration.MailBoxName.Equals(mbname)).ToList();
            }
        }

        [Route("mailboxes/{mbname}")]
        [HttpPost]
        public EmailFilter AddEmailFilter(string mbname, [FromBody] EmailFilterDto filterToAdd)
        {
            var filter = Mapper.Map<EmailFilter>(filterToAdd);
            ImapMailBoxConfiguration conf;

            if (filter.MoveToFolder == null)
            {
                filter.MoveToFolder = "";
            }

            if (filter.ForwardToAddress == null)
            {
                filter.ForwardToAddress = "";
            }

            using (var ctx = new MailModelContainer())
            {
                conf = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.MailBoxName.Equals(mbname));

                if (conf == null) return null;

                conf.EmailFilters.Add(filter);

                ctx.SaveChanges();
            }

            Task.Factory.StartNew(() => InboxWatcher.ConfigureMailBox(conf));

            return filter;
        }

        [Route("mailboxes/{mbname}/{id}")]
        [HttpDelete]
        public EmailFilter DeleteEmailFilter(string mbname, int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.EmailFilters.FirstOrDefault(x => x.Id == id);
                ctx.EmailFilters.Remove(selection);
                ctx.SaveChanges();
                return selection;
            }
        }

        [Route("mailboxes/{mbname}/{id}")]
        [HttpPut]
        public EmailFilter ChangeEmailFilter(string mbname, [FromBody] EmailFilterDto filterToAdd, int id)
        {
            var filter = Mapper.Map<EmailFilter>(filterToAdd);

            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.EmailFilters.First(x => x.Id == id);

                selection.FilterName = filter.FilterName;
                selection.ForwardThis = filter.ForwardThis;
                selection.ForwardToAddress = filter.ForwardToAddress;
                selection.MoveToFolder = filter.MoveToFolder;
                selection.SentFromContains = filter.SentFromContains;
                selection.SubjectContains = filter.SubjectContains;

                ctx.SaveChanges();
                return selection;
            }
        }
    }
}