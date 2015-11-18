using System.Web.Http;
using Newtonsoft.Json.Serialization;
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

            appBuilder.UseWebApi(config);

            var serSettings = config.Formatters.JsonFormatter.SerializerSettings;
            var contractResolver = (DefaultContractResolver) serSettings.ContractResolver;
            contractResolver.IgnoreSerializableAttribute = true;

            config.EnsureInitialized();
        }
    }
}