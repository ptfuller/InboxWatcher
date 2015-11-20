using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace InboxWatcher
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            AfterInstall += OnAfterInstall;
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            //check for presence of local db

            var version12 =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\12.0");

            var version11 = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\11.0");

            if (version12 == null && version11 == null)
            {
                var path = Path.Combine(Path.GetTempPath(), "SqlLocalDB.msi");
                Process.Start(path);
            }
        }

        private void OnAfterInstall(object sender, InstallEventArgs installEventArgs)
        {
            //using (ServiceController sc = new ServiceController(serviceInstaller1.ServiceName))
            //{
            //    sc.Start();
            //}

            //Process.Start("http://localhost:9000/config/ui");
        }
    }
}