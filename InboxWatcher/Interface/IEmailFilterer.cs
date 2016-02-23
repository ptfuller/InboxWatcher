using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit;

namespace InboxWatcher.ImapClient
{
    public interface IEmailFilterer
    {
        Task FilterAllMessages(IEnumerable<IMessageSummary> messages);
    }
}