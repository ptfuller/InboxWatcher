using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using NLog;
using WebGrease.Css.Extensions;

namespace InboxWatcher.ImapClient
{
    public class EmailFilterer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ImapMailBox _attachedMailBox;
        private readonly List<EmailFilter> _emailFilters = new List<EmailFilter>();

        public EmailFilterer(ImapMailBox attachedMailBox)
        {
            _attachedMailBox = attachedMailBox;

            _attachedMailBox.NewMessageReceived += FilterOnMessageReceived;

            using (var ctx = new MailModelContainer())
            {
                _emailFilters.AddRange(ctx.EmailFilters.ToList());
            }
        }

        public async Task FilterAllMessages(IEnumerable<IMessageSummary> messages)
        {
            Debug.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering Messages");
            messages.ForEach(FilterMessage);
        }

        private async void FilterOnMessageReceived(object email, EventArgs args)
        {
            if (email == null) return;
            var receivedMessage = (IMessageSummary) email;
            await Task.Delay(1500);
            FilterMessage(receivedMessage);
        }

        private async void FilterMessage(IMessageSummary msgSummary)
        {

            try
            {
                foreach (var filter in _emailFilters)
                {
                    //check subject contains
                    if (!string.IsNullOrEmpty(filter.SubjectContains))
                    {
                        if (!string.IsNullOrEmpty(msgSummary.Envelope.Subject) &&
                            !msgSummary.Envelope.Subject.ToLower().Contains(filter.SubjectContains.ToLower())) continue;
                    }

                    //check sender's address
                    if (!string.IsNullOrEmpty(filter.SentFromContains))
                    {
                        if (!msgSummary.Envelope.From[0].ToString().ToLower().Contains(filter.SentFromContains))
                            continue;
                    }

                    //get the message
                    var theMessage = await _attachedMailBox.GetMessage(msgSummary.UniqueId.Id);

                    if (theMessage == null) return;
                    
                    if (filter.ForwardThis)
                    {
                        Debug.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering {theMessage.Subject}");

                        //forward the message
                        if (!await _attachedMailBox.SendMail(theMessage, msgSummary.UniqueId.Id, filter.ForwardToAddress, false))
                        {
                            return;
                        }
                    }

                    //move the message
                    _attachedMailBox.MoveMessage(msgSummary, filter.MoveToFolder, filter.FilterName);

                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
    }
}