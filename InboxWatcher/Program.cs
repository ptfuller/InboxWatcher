using System.ServiceProcess;

namespace InboxWatcher
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new InboxWatcher()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}