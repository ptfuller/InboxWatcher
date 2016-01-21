﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using NLog;

namespace InboxWatcher.ImapClient
{
    public class EmailFilterer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ImapMailBox _attachedMailBox;
        private readonly List<EmailFilter> _emailFilters = new List<EmailFilter>();
        private bool _currentlyFiltering;

        public EmailFilterer(ImapMailBox attachedMailBox)
        {
            _attachedMailBox = attachedMailBox;

            _attachedMailBox.NewMessageReceived -= FilterOnMessageReceived;
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

            Trace.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering Messages");

            try
            {
                var messageSummaries = messages.ToList();

                for (var i = 0; i < messageSummaries.Count; i++)
                {
                    await FilterMessage(messageSummaries[i]);
                }
            }
            catch
            {
                
            }

            _currentlyFiltering = false;
            Trace.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering All Done");
        }

        private async void FilterOnMessageReceived(object email, EventArgs args)
        {
            if (email == null) return;
            await HandleFilteringMessage(email);
        }

        private async Task HandleFilteringMessage(object email)
        {
            var msg = (IMessageSummary) email;

            await Task.Run(async () => { await FilterMessage(msg); });
        }

        private async Task FilterMessage(IMessageSummary msgSummary)
        {
            //Trace.WriteLine($"{_attachedMailBox.MailBoxName}: checking to see if message with subject:{msgSummary.Envelope.Subject} should be filtered");

            await Task.Delay(1000);

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

                    //Trace.WriteLine($"{_attachedMailBox.MailBoxName}: Filtering {msgSummary.Envelope.Subject}");

                    if (filter.ForwardThis)
                    {
                        //get the message
                        var theMessage = await _attachedMailBox.GetMessage(msgSummary.UniqueId.Id);

                        if (theMessage == null) return;

                        //forward the message
                        while (!await _attachedMailBox.SendMail(theMessage, msgSummary.UniqueId.Id, filter.ForwardToAddress, false))
                        {
                            //Trace.WriteLine($"{_attachedMailBox.MailBoxName}: problem sending filtered message {theMessage.Subject} - waiting...");
                            await Task.Delay(10000);
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