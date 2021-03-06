﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using InboxWatcher.DTO;
using InboxWatcher.Interface;

namespace InboxWatcher.WebAPI.Controllers
{
    [RoutePrefix("configs")]
    public class ConfigurationController : ApiController
    {
        [Route("ui", Name = "ui")]
        public HttpResponseMessage Get()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            
            var configFile = File.ReadAllText(Path.Combine(InboxWatcher.ResourcePath, "configuration.html"));

            var content = new StringContent(configFile);

            response.Content = content;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            
            return response;
        }

        [Route("{id:int}")]
        public IClientConfiguration Get(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                ctx.Configuration.ProxyCreationEnabled = false;
                var selection = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == id);
                if (selection != null) return selection;

                return new ImapMailBoxConfiguration();
            }
        }

        [Route("")]
        [HttpGet]
        public IEnumerable<IClientConfiguration> GetMailBoxConfigurations()
        {
            using (var ctx = new MailModelContainer())
            {
                var results = new List<IClientConfiguration>();
                foreach (var selection in ctx.ImapMailBoxConfigurations)
                {
                    results.Add(new ClientConfigurationDto(selection));
                }
                return results;
            }
        }

        [Route("")]
        [HttpPost]
        public IClientConfiguration Post(ClientConfigurationDto conf)
        {
            IClientConfiguration result;
            using (var ctx = new MailModelContainer())
            {
                result = ctx.ImapMailBoxConfigurations.Add(conf.GetMailBoxConfiguration());
                ctx.SaveChanges();
            }

            Task.Factory.StartNew(async () => { await InboxWatcher.ConfigureMailBox(result); });

            return new ClientConfigurationDto(result);
        }

        [Route("{id:int}")]
        [HttpPut]
        public IClientConfiguration Put(ClientConfigurationDto conf)
        {
            IClientConfiguration selection;

            using (var ctx = new MailModelContainer())
            {
                selection = ctx.ImapMailBoxConfigurations.Find(conf.Id);
                ctx.Entry(selection).CurrentValues.SetValues(conf);
                ctx.SaveChanges();
            }

            InboxWatcher.MailBoxes.Remove(conf.Id);
            Task.Factory.StartNew(async () => { await InboxWatcher.ConfigureMailBox(conf); });
            return selection;
        }

        [Route("{id:int}")]
        [HttpDelete]
        public HttpResponseMessage DeleteMailBox(int id)
        {
            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.ImapMailBoxConfigurations.First(x => x.Id == id);

                InboxWatcher.MailBoxes.Remove(selection.Id);

                ctx.ImapMailBoxConfigurations.Attach(selection);
                ctx.ImapMailBoxConfigurations.Remove(selection);
                ctx.SaveChanges();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private HttpResponseMessage RedirectToUi(HttpRequestMessage request)
        {
            var response = request.CreateResponse(HttpStatusCode.Redirect);
            var url = request.RequestUri.GetLeftPart(UriPartial.Authority);
            response.Headers.Location = new Uri(url + "/config/ui");

            return response;
        }

        [Route("backup")]
        [HttpGet]
        public string Backup()
        {
            return InboxWatcher.BackupDatabase();
        }

        [Route("reset/{mbName}")]
        [HttpGet]
        public HttpResponseMessage ResetMailBox(string mbName)
        {
            using (var ctx = new MailModelContainer())
            {
                var selection = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.MailBoxName.Equals(mbName));

                if (selection == null) return new HttpResponseMessage(HttpStatusCode.NotFound);
                
                Task.Factory.StartNew(async () => { await InboxWatcher.ConfigureMailBox(selection); });

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}