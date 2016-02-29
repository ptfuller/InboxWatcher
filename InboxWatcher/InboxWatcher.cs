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
using Ninject;
using Ninject.Extensions.Factory;
using Ninject.Parameters;
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

        private static IKernel _kernel;
        private IKernel Kernel { get; set; }

        protected override void OnStart(string[] args)
        {
            Setup();
        }

        private async Task<Task> Setup()
        {
            //Debugger.Launch();

            _kernel = ConfigureNinject();
            Kernel = _kernel;

            ConfigureAutoMapper();

            Trace.Listeners.Add(new SignalRTraceListener());
            Trace.Listeners.Add(new NLogTraceListener());

            ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

            StartWebApi();

            logger.Info("Inbox Watcher Started");
            Trace.WriteLine("Inbox Watcher Started");

            return ConfigureMailBoxes();
        }

        private IKernel ConfigureNinject()
        {
            _kernel = new StandardKernel();

            _kernel.Bind<IClientConfiguration>().To<ImapClientConfiguration>();
            _kernel.Bind<IImapMailBox>().To<ImapMailBox>();
            _kernel.Bind<IImapFactory>().ToFactory();
            
            return _kernel;
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

        public static List<ImapMailBoxConfiguration> GetConfigs()
        {
            using (var ctx = _kernel.Get<MailModelContainer>())
            {
                return ctx.ImapMailBoxConfigurations.ToList();
            }
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

            using (var ctx = _kernel.Get<MailModelContainer>())
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

            //create mailboxes via ninject
            var director = _kernel.Get<ImapClientFactory>(new ConstructorArgument("configuration", conf));
            var mailbox = _kernel.Get<ImapMailBox>(new ConstructorArgument("icd", director));

            foreach (var action in SetupNotifications(conf.Id))
            {
                mailbox.AddNotification(action);
            }

            MailBoxes.Add(mailbox.MailBoxId, mailbox);

            await mailbox.Setup();

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.SetupMailboxes();
        }

        internal async Task ConfigureMailBoxes()
        {
            MailBoxes = new Dictionary<int, ImapMailBox>();
            var tasks = new List<Task>();

            foreach(var config in GetConfigs())
            {
                var mailbox = _kernel.Get<ImapMailBox>(new ConstructorArgument("configuration", config));

                MailBoxes.Add(mailbox.MailBoxId, mailbox);
                tasks.Add(mailbox.Setup());

                foreach (var action in SetupNotifications(mailbox.MailBoxId))
                {
                    mailbox.AddNotification(action);
                }
            }

            await Task.WhenAll(tasks);

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