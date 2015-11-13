using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Web.Http;
using InboxWatcher.Notifications;
using InboxWatcher.WebAPI;
using MailKit;
using Microsoft.Owin.Hosting;
using Owin;

namespace InboxWatcher
{
    public partial class InboxWatcher : ServiceBase
    {
        public InboxWatcher()
        {
            InitializeComponent();
        }

        private static List<ImapMailBox> _mailBoxes = new List<ImapMailBox>();

        protected override void OnStart(string[] args)
        {
            //todo remove
            Debugger.Launch();

            StartWebApi();

            //get configuration objects from database
            var configs = GetConfigs();

            //setup each ImapMailBox and add it to the list of mailboxes
            foreach (var clientConfiguration in configs)
            {
                var director = new ImapClientDirector(clientConfiguration);
                var mailbox = new ImapMailBox(director, clientConfiguration.MailBoxName, SetupNotifications());
                _mailBoxes.Add(mailbox);
            }

            foreach (var imapMailBox in _mailBoxes)
            {
                imapMailBox.NewMessageReceived += (sender, eventArgs) =>
                {
                    var summary = sender as IMessageSummary;
                    Debug.WriteLine(imapMailBox.MailBoxName + ": Message Received: " + summary.Envelope.Subject);
                    Debug.WriteLine("ID: " + summary.Envelope.MessageId);
                };

                imapMailBox.MessageRemoved += (sender, eventArgs) =>
                {
                    var summary = sender as IMessageSummary;
                    Debug.WriteLine(imapMailBox.MailBoxName + ": Message Removed " + summary.Envelope.Subject);
                    Debug.WriteLine("ID: " + summary.Envelope.MessageId);
                };
            }
        }

        protected override void OnStop()
        {
        }


        private void StartWebApi()
        {
            string baseAddress = "http://localhost:9000/";
            WebApp.Start<WebApiStartup>(baseAddress);
        }

        private IEnumerable<IClientConfiguration> GetConfigs()
        {
            var configs = new List<IClientConfiguration>();

            using (var ctx = new MailModelContainer())
            {
                var cfgs = ctx.ImapMailBoxConfigurations.ToList();
                configs.AddRange(cfgs);
            }

            return configs;
        }

        private INotificationAction SetupNotifications()
        {
            var notification = new HttpNotification()
                .WithContentType("application/x-www-form-urlencoded")
                .WithMethod(WebRequestMethods.Http.Post)
                .WithUrl("http://localhost:9000/api/Hello");

            return notification;
        }
    }
}