using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using AutoMapper;
using InboxWatcher.DTO;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using InboxWatcher.Notifications;
using InboxWatcher.Properties;
using InboxWatcher.WebAPI;
using InboxWatcher.WebAPI.Controllers;
using MailKit;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;
using NLog;

namespace InboxWatcher
{
    public partial class InboxWatcher : ServiceBase
    {
        public InboxWatcher()
        {
            InitializeComponent();
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     All running ImapMailBoxes are held in this list
        /// </summary>
        public static Dictionary<int, ImapMailBox> MailBoxes { get; set; }

        /// <summary>
        ///     The path to the html, js, and css resources for the UI
        /// </summary>
        public static string ResourcePath { get; internal set; }

        private static IEnumerable<IClientConfiguration> Configs
        {
            get
            {
                using (var ctx = new MailModelContainer())
                {
                    return ctx.ImapMailBoxConfigurations.ToList();
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            Debugger.Launch();

            ConfigureAutoMapper();

            Trace.Listeners.Add(new SignalRTraceListener());
            Trace.Listeners.Add(new NLogTraceListener());

            ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

            StartWebApi();

            Task.Run(async () => { await ConfigureMailBoxes(); });

            logger.Info("Inbox Watcher Started");
            Trace.WriteLine("Inbox Watcher Started");
        }

        private void ConfigureAutoMapper()
        {
            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Email, EmailDto>()
                    .ForMember(x => x.EmailLogs, opt => opt.Ignore())
                    .ForMember(x => x.ImapMailBoxConfiguration, opt => opt.Ignore());

                cfg.CreateMap<EmailFilterDto, EmailFilter>()
                    .ForMember(x => x.ImapMailBoxConfiguration, opt => opt.Ignore());
            });
        }

        protected override void OnStop()
        {
            Trace.WriteLine("Service shutting down");
        }


        private void StartWebApi()
        {
            var baseAddress = Settings.Default.HostName;
            WebApp.Start<WebApiStartup>(baseAddress);
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


        internal static async Task ConfigureMailBox(IClientConfiguration conf)
        {
            var selectedMailBox = MailBoxes.FirstOrDefault(x => x.Key == conf.Id).Value;

            //changing an existing config
            if (selectedMailBox != null)
            {
                MailBoxes.Remove(selectedMailBox.MailBoxId);
            }

            var director = new ImapClientDirector(conf);
            var mailbox = new ImapMailBox(director, conf);

            foreach (var action in SetupNotifications(conf.Id))
            {
                mailbox.AddNotification(action);
            }

            await mailbox.Setup();

            MailBoxes.Add(mailbox.MailBoxId, mailbox);

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.SetupMailboxes();
        }

        internal static async Task ConfigureMailBoxes()
        {
            MailBoxes = new Dictionary<int, ImapMailBox>();

            //get configuration objects from database
            var configs = Configs;

            //setup each ImapMailBox and add it to the list of mailboxes
            var setupTasks = configs.Select(clientConfiguration => Task.Run(async () =>
            {
                Trace.WriteLine($"Setting up {clientConfiguration.MailBoxName}");
                var director = new ImapClientDirector(clientConfiguration);
                var mailbox = new ImapMailBox(director, clientConfiguration);

                await mailbox.Setup();

                foreach (var action in SetupNotifications(clientConfiguration.Id))
                {
                    mailbox.AddNotification(action);
                }

                MailBoxes.Add(mailbox.MailBoxId, mailbox);
            })).ToList();
            
            await Task.WhenAll(setupTasks);

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.SetupMailboxes();

            //todo remove this - it's for debugging
            foreach (var imapMailBox in MailBoxes.Values)
            {
                imapMailBox.NewMessageReceived += (sender, eventArgs) =>
                {
                    var summary = sender as IMessageSummary;
                    Trace.WriteLine(imapMailBox.MailBoxName + ": Message Received: " + summary.Envelope.Subject);
                    Trace.WriteLine("ID: " + summary.Envelope.MessageId);
                };

                imapMailBox.MessageRemoved += (sender, eventArgs) =>
                {
                    var summary = sender as IMessageSummary;
                    Trace.WriteLine(imapMailBox.MailBoxName + ": Message Removed " + summary.Envelope.Subject);
                    Trace.WriteLine("ID: " + summary.Envelope.MessageId);
                };
            }
        }
    }
}