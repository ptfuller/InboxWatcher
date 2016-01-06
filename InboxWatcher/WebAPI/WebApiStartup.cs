using System.Security.Policy;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;
using Owin;

namespace InboxWatcher.WebAPI
{
    public class WebApiStartup
    {
        // This code configures Web API. The WebApiStartup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();

            

            //attribute routing
            config.MapHttpAttributeRoutes();

            //convention based routing
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Routes.MapHttpRoute(
                name: "Error404",
                routeTemplate: "{*url}",
                defaults: new {controller = "Dashboard", action = "Dashboard"});

            appBuilder.Map("/signalr", map =>
            {
                var hubConfiguration = new HubConfiguration
                {

                };

                map.RunSignalR(hubConfiguration);
            });

            config.Formatters.Remove(config.Formatters.XmlFormatter);
            //config.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            config.EnsureInitialized();

            appBuilder.UseWebApi(config);
        }
    }
}