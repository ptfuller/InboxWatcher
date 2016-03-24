using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using AutoMapper;
using InboxWatcher.DTO;
using InboxWatcher.ImapClient;
using InboxWatcher.Interface;
using InboxWatcher.Properties;
using InboxWatcher.WebAPI;
using InboxWatcher.WebAPI.Controllers;
using MailKit;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;
using Ninject;
using Ninject.Parameters;
using NLog;
using Timer = System.Timers.Timer;

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
        public static Dictionary<int, IImapMailBox> MailBoxes { get; set; }

        /// <summary>
        ///     The path to the html, js, and css resources for the UI
        /// </summary>
        public static string ResourcePath { get; internal set; }

        private static IKernel _kernel;
        private Timer _backupTimer;

        protected override void OnStart(string[] args)
        {
            Setup();

            ConfigureAutoMapper();

            _backupTimer = new Timer(1000 * 60 * 60); //1 hour
            _backupTimer.Elapsed += BackupTimerOnElapsed;
            _backupTimer.AutoReset = false;
            _backupTimer.Start();
        }

        private void BackupTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            Trace.WriteLine($"{DateTime.Now.ToString("s")} Backup Status: {BackupDatabase()}");
            _backupTimer.Stop();
            _backupTimer.Start();
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

        private async Task<Task> Setup()
        {
            //Debugger.Launch();

            _kernel = ConfigureNinject();

            //ConfigureAutoMapper();

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
            _kernel.Bind<IImapMailBox>().ToProvider(new ImapMailBoxProvider());
            _kernel.Bind<IImapFactory>().To<ImapClientFactory>();
            _kernel.Bind<IMailBoxLogger>().To<MailBoxLogger>();
            _kernel.Bind<IImapWorker>().To<ImapWorker>();
            _kernel.Bind<IImapIdler>().To<ImapIdler>();
            _kernel.Bind<IEmailSender>().To<EmailSender>();
            _kernel.Bind<IEmailFilterer>().To<EmailFilterer>();
            
            return _kernel;
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


        private static IEnumerable<INotificationAction> SetupNotifications(int imapMailBoxConfigId)
        {
            var notifications = new List<INotificationAction>();

            using (var ctx = _kernel.Get<MailModelContainer>())
            {
                var configurations =
                    ctx.NotificationConfigurations.Where(x => x.ImapMailBoxConfigurationId == imapMailBoxConfigId);

                foreach (var configuration in configurations)
                {
                    var t = Type.GetType(configuration.NotificationType);

                    if (t == null) continue;
                    var action = (INotificationAction) Activator.CreateInstance(t);
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

                Trace.WriteLine($"*InboxWatcher* : Mailbox with ID:{selectedMailBox.MailBoxId} removed - {MailBoxes.Count} Mailboxes remain");

                selectedMailBox.Dispose();
                selectedMailBox = null;
            }

            //create mailbox via ninject
            var mailbox = _kernel.Get<IImapMailBox>(new ConstructorArgument("configuration", conf));

            foreach (var action in SetupNotifications(conf.Id))
            {
                mailbox.AddNotification(action);
            }

            MailBoxes.Add(mailbox.MailBoxId, mailbox);

            await mailbox.Setup();

            //add the trace event listeners for received and removed to the new mailbox
            mailbox.NewMessageReceived += (sender, eventArgs) =>
            {
                var summary = sender as IMessageSummary;
                Trace.WriteLine(mailbox.MailBoxName + ": Message Received: " + summary.Envelope.Subject);
                Trace.WriteLine("ID: " + summary.Envelope.MessageId);
            };

            mailbox.MessageRemoved += (sender, eventArgs) =>
            {
                var summary = sender as IMessageSummary;
                Trace.WriteLine(mailbox.MailBoxName + ": Message Removed " + summary.Envelope.Subject);
                Trace.WriteLine("ID: " + summary.Envelope.MessageId);
            };

            var ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
            ctx.Clients.All.SetupMailboxes();
        }


        internal async Task ConfigureMailBoxes()
        {
            MailBoxes = new Dictionary<int, IImapMailBox>();
            var tasks = new List<Task>();

            foreach(var config in GetConfigs())
            {
                var mailbox = _kernel.Get<IImapMailBox>(new ConstructorArgument("configuration", config));

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

        public static string BackupDatabase()
        {
            using (var ctx = new MailModelContainer())
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var dbLocation = Path.Combine(assemblyLocation, "InboxWatcher.mdf");
                var backupPath = Path.Combine(assemblyLocation, "Backups", "InboxWatcher.mdf");

                if (!Directory.Exists(Path.Combine(assemblyLocation, "Backups")))
                {
                    Directory.CreateDirectory(Path.Combine(assemblyLocation, "Backups"));
                }

                try
                {
                    ctx.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, $"BACKUP DATABASE [{dbLocation}] TO DISK=N'{backupPath}' " +
                                                                                                 $"WITH FORMAT, MEDIANAME='InboxWatcherBackup', MEDIADESCRIPTION='Media set for [{dbLocation}]'");
                }
                catch (Exception ex)
                {
                    return ex.ToString();
                }

                return "Success";
            }
        }
    }
}