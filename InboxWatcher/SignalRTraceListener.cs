using System.Diagnostics;
using InboxWatcher.WebAPI.Controllers;
using Microsoft.AspNet.SignalR;

namespace InboxWatcher
{
    public class SignalRTraceListener : TraceListener
    {
        private IHubContext ctx;

        public SignalRTraceListener()
        {
            ctx = GlobalHost.ConnectionManager.GetHubContext<SignalRController>();
        }

        public override void Write(string message)
        {
            ctx.Clients.All.DisplayToTerminal(message);
        }

        public override void WriteLine(string message)
        {
            ctx.Clients.All.DisplayToTerminal(message);
        }
    }
}