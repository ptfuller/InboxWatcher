using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using NLog;

namespace InboxWatcher.ImapClient
{
    public class EmailFilterer : IEmailFilterer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly IImapMailBox _attachedMailBox;
        private readonly List<EmailFilter> _emailFilters = new List<EmailFilter>();
        private bool _currentlyFiltering;

        public EmailFilterer(IImapMailBox attachedMailBox)
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
            if (_currentlyFiltering) return;
            _currentlyFiltering = true;

            var messageSummaries = messages.ToList();

            Trace.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering {messageSummaries.Count()} Messages");

            try
            {
                await Task.WhenAll(messageSummaries.Select(FilterMessage).ToArray());
            }
            catch
            {
                // ignored
            }

            _currentlyFiltering = false;
            Trace.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering All Done");
        }

        private async void FilterOnMessageReceived(object email, EventArgs args)
        {
            if (email == null) return;
            if (_currentlyFiltering) return;
            await HandleFilteringMessage(email);
        }

        private async Task HandleFilteringMessage(object email)
        {
            var msg = (IMessageSummary) email;

            await FilterMessage(msg);
        }

        private async Task FilterMessage(IMessageSummary msgSummary)
        {
            await Task.Delay(1000); //let enough time pass so that the message is logged before it is filtered

            try
            {
                foreach (var filter in _emailFilters)
                {
                    //check subject contains
                    if (!string.IsNullOrEmpty(filter.SubjectContains))
                    {
                        if (string.IsNullOrEmpty(msgSummary.Envelope.Subject) ||
                            !msgSummary.Envelope.Subject.ToLower().Contains(filter.SubjectContains.ToLower())) continue;
                    }

                    //check sender's address
                    if (!string.IsNullOrEmpty(filter.SentFromContains))
                    {
                        if (!msgSummary.Envelope.From[0].ToString().ToLower().Contains(filter.SentFromContains))
                            continue;
                    }
                    
                    if (filter.ForwardThis)
                    {
                        //get the message
                        var theMessage = await _attachedMailBox.GetMessage(msgSummary.UniqueId.Id);

                        if (theMessage == null) return;

                        //forward the message
                        if (!await _attachedMailBox.SendMail(theMessage, msgSummary.UniqueId.Id, filter.ForwardToAddress, false))
                        {
                            return;
                        }
                    }

                    //move the message
                    await _attachedMailBox.MoveMessage(msgSummary, filter.MoveToFolder, "Filter: " + filter.FilterName);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
    }
}