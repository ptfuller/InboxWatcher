using System;
using System.Diagnostics;
using Microsoft.AspNet.SignalR;

namespace InboxWatcher.WebAPI.Controllers
{
    public class SignalRController : Hub
    {
       public void Refresh()
        {
            Clients.All.FreshenMailBox();
        }
    }
}