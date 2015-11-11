using System.ComponentModel;
using System.Configuration.Install;

namespace InboxWatcher
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}