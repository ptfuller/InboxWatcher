using System.Diagnostics;
using Microsoft.AspNet.SignalR;

namespace InboxWatcher.WebAPI.Controllers
{
    public class SignalRController : Hub
    {
        public void Hello()
        {
            Clients.All.hello("Hello from SignalR!");
        }

        public void Send(string message)
        {
            Debug.WriteLine(message);
        }
    }
}