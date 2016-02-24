using InboxWatcher.Interface;
using Ninject.Activation;

namespace InboxWatcher
{
    public class ConfigurationProvider : Provider<IClientConfiguration>
    {
        protected override IClientConfiguration CreateInstance(IContext context)
        {
            if (InboxWatcher.ClientConfigurations.Count == 0)
            {
                foreach (var config in InboxWatcher.ClientConfigurations)
                {
                    InboxWatcher.ClientConfigurations.Enqueue(config);
                }
            }
            
            return InboxWatcher.ClientConfigurations.Dequeue();
        }
    }
}