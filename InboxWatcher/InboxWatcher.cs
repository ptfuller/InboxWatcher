using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.ServiceProcess;
using System.Web.Http;
using AutoMapper;
using InboxWatcher.DTO;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using InboxWatcher.Notifications;
using InboxWatcher.WebAPI;
using MailKit;
using Microsoft.Owin.Hosting;
using Owin;

namespace InboxWatcher
{
    public partial class InboxWatcher : ServiceBase
    {
        public static List<ImapMailBox> MailBoxes { get; set; }
        public static string ResourcePath { get; private set; }

        public InboxWatcher()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //todo remove
            Debugger.Launch();

            ConfigureAutoMapper();

            ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

            StartWebApi();
            
            ConfigureMailBoxes();
        }

        private void ConfigureAutoMapper()
        {
            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Email, EmailDto>()
                .ForMember(x => x.EmailLogs, opt => opt.Ignore())
                .ForMember(x => x.ImapMailBoxConfiguration, opt => opt.Ignore());
            });
        }

        protected override void OnStop()
        {
            
        }


        private void StartWebApi()
        {
            string baseAddress = "http://localhost:9000/";
            WebApp.Start<WebApiStartup>(baseAddress);
        }

        private static IEnumerable<IClientConfiguration> GetConfigs()
        {
            using (var ctx = new MailModelContainer())
            {
                return ctx.ImapMailBoxConfigurations.ToList();
            }
        }

        private static IEnumerable<AbstractNotification> SetupNotifications(int imapMailBoxConfigId)
        {
            var notifications = new List<AbstractNotification>();

            using (var ctx = new MailModelContainer())
            {
                var configurations =
                    ctx.NotificationConfigurations.Where(x => x.ImapMailBoxConfigurationId == imapMailBoxConfigId);

                foreach (var configuration in configurations)
                {
                    var t = Type.GetType(configuration.NotificationType);

                    if (t == null) continue;
                    var action = (AbstractNotification) Activator.CreateInstance(t);
                    notifications.Add(action.DeSerialize(configuration.ConfigurationXml));
                }
            }

            return notifications;
        }

        public static void ConfigureMailBoxes()
        {
            MailBoxes = new List<ImapMailBox>();

            //get configuration objects from database
            var configs = GetConfigs();

            //setup each ImapMailBox and add it to the list of mailboxes
            foreach (var clientConfiguration in configs)
            {
                var director = new ImapClientDirector(clientConfiguration);
                var mailbox = new ImapMailBox(director, clientConfiguration);

                foreach (var action in SetupNotifications(clientConfiguration.Id))
                {
                    mailbox.AddNotification(action);
                }

                MailBoxes.Add(mailbox);
            }
            
            //todo remove this - it's for debugging
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