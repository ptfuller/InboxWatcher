using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public static List<ImapMailBox> MailBoxes { get; set; } = new List<ImapMailBox>();

        protected override void OnStart(string[] args)
        {
            //todo remove
            Debugger.Launch();

            StartWebApi();

            ConfigureMailBoxes();
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
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.ToList();
            }
        }

        private IEnumerable<INotificationAction> SetupNotifications()
        {
            var notifications = new List<INotificationAction>();
            var notification = new HttpNotification()
                .WithContentType("application/x-www-form-urlencoded")
                .WithMethod(WebRequestMethods.Http.Post)
                .WithUrl("http://localhost:9000/api/Hello");


            notifications.Add(notification);

            return notifications;
        }

        private void ConfigureMailBoxes()
        {
            //get configuration objects from database
            var configs = GetConfigs();

            //setup each ImapMailBox and add it to the list of mailboxes
            foreach (var clientConfiguration in configs)
            {
                var director = new ImapClientDirector(clientConfiguration);
                var mailbox = new ImapMailBox(director, clientConfiguration.MailBoxName, SetupNotifications());
                MailBoxes.Add(mailbox);
            }

            foreach (var imapMailBox in MailBoxes)
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
    }
}