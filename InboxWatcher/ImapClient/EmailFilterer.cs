﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MimeKit;
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

            #pragma warning disable 4014
            HandleFilteringMessage(email);
            #pragma warning restore 4014
        }

        private async Task HandleFilteringMessage(object email)
        {
            var msg = (IMessageSummary) email;

            await Task.Delay(1000); //let enough time pass so that the message is logged before it is filtered
            await FilterMessage(msg);
        }

        private async Task FilterMessage(IMessageSummary msgSummary)
        {
            //to move all filtered emails at once and prevent multiple individual calls to movemessage
            //dictionary key is the filter name, list of message summaries to move to the filter's folder
            var emailsToMove = new Dictionary<string, List<IMessageSummary>>();

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
                    //await _attachedMailBox.MoveMessage(msgSummary, filter.MoveToFolder, "Filter: " + filter.FilterName);
                    if (emailsToMove.ContainsKey(filter.MoveToFolder))
                    {
                        emailsToMove[filter.MoveToFolder].Add(msgSummary);
                    }
                    else
                    {
                        var summaryList = new List<IMessageSummary> {msgSummary};
                        emailsToMove.Add(filter.MoveToFolder, summaryList);
                    }
                }

                await _attachedMailBox.MoveMessage(emailsToMove);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{_attachedMailBox.MailBoxName} EmailFilterer exception: {ex.Message}");
            }
        }
    }
}