using System.Linq;
using InboxWatcher.Interface;
using Ninject;
using Ninject.Activation;
using Ninject.Parameters;
using Ninject.Planning.Targets;

namespace InboxWatcher.ImapClient
{
    public class ImapMailBoxProvider : Provider<IImapMailBox>
    {
        protected override IImapMailBox CreateInstance(IContext context)
        {
            //get the initial passed in configuration value
            var parms = context.Parameters.First();
            var configuration = (IClientConfiguration) parms.GetValue(context, null);

            //configuration argument
            var conArgument = new ConstructorArgument("config", configuration);

            //get the imap factory
            var factory = context.Kernel.Get<IImapFactory>(conArgument);

            //factory argument
            var factArgument = new ConstructorArgument("factory", factory);

            //get worker, idler, sender
            var worker = context.Kernel.Get<IImapWorker>(factArgument);
            var idler = context.Kernel.Get<IImapIdler>(factArgument);
            var sender = context.Kernel.Get<IEmailSender>(factArgument);

            //get logger
            var mbLogger = context.Kernel.Get<IMailBoxLogger>(conArgument);
            
            //construct and return
            return new ImapMailBox(configuration, mbLogger, worker, idler, sender);
        }
    }
}