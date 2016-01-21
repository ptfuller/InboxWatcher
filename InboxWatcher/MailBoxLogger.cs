using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using MimeKit;
using MimeKit.Text;

namespace InboxWatcher
{
    public class MailBoxLogger
    {
        private IClientConfiguration _config;

        public MailBoxLogger(IClientConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> LogEmailReceived(IMessageSummary summary)
        {

            if (string.IsNullOrEmpty(summary.Envelope.MessageId)) return false;

            using (var context = new MailModelContainer())
            {
                //if it's already in the DB we're not going to log it received
                if (context.Emails.Any(x => x.EnvelopeID.Equals(summary.Envelope.MessageId))) return false;

                //generate new Email and EmailLogs
                var email = new Email()
                {
                    BodyText = "",
                    EnvelopeID = summary.Envelope.MessageId,
                    InQueue = true,
                    MarkedAsRead = false,
                    Minutes = (int) (DateTime.Now.ToUniversalTime() - summary.Date.ToUniversalTime()).TotalMinutes,
                    Sender = summary.Envelope.From.ToString(),
                    Subject = string.IsNullOrEmpty(summary.Envelope.Subject) ? "" :summary.Envelope.Subject,
                    TimeReceived = summary.Date.LocalDateTime,
                    ImapMailBoxConfigurationId = _config.Id
                };

                var el = new EmailLog();
                el.Action = "Received";
                el.TimeActionTaken = DateTime.Now.ToLocalTime();
                el.TakenBy = "";
                el.Email = email;

                email.EmailLogs.Add(el);

                try
                {
                    context.Emails.Add(email);
                    await context.SaveChangesAsync();
                }
                catch (DbEntityValidationException ex)
                {
                    throw ex;
                }
            }

            return true;
        }

        public async Task LogEmailRemoved(IMessageSummary email)
        {
            using (var context = new MailModelContainer())
            {
                var selectedEmail =
                    context.Emails.Include(e => e.EmailLogs).FirstOrDefault(
                        x => _config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(email.Envelope.MessageId));

                if (selectedEmail == null) return;

                if (selectedEmail.EmailLogs.Any(x => x.Action.Contains("Sent") && x.Action.ToLower().Contains("moved") || x.Action.Contains("Removed")))
                {
                    return;
                }
                

                await LogEmailChanged(email, "", "Removed");

                selectedEmail.Minutes = (int) (DateTime.Now.ToUniversalTime() - selectedEmail.TimeReceived.ToUniversalTime()).TotalMinutes;
                selectedEmail.InQueue = false;
                

                await context.SaveChangesAsync();
            }
        }

        public async Task LogEmailChanged(IMessageSummary email, string actionTakenBy, string action)
        {
            using (var context = new MailModelContainer())
            {

                var selectedEmails =
                    context.Emails.Include(x => x.EmailLogs).Where(
                        x => _config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(email.Envelope.MessageId));

                var newLogs = new List<EmailLog>();

                foreach (var em in selectedEmails)
                {
                    var el = new EmailLog
                    {
                        Action = action,
                        TimeActionTaken = DateTime.Now.ToLocalTime(),
                        Email = em,
                        TakenBy = actionTakenBy
                    };
                    newLogs.Add(el);
                }

                context.EmailLogs.AddRange(newLogs);
                await context.SaveChangesAsync();
            }
        }

        public async Task LogEmailChanged(string messageId, string actionTakenBy, string action)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedEmail =
                    ctx.Emails.Include(x => x.EmailLogs).FirstOrDefault(
                        x => _config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(messageId));

                if (selectedEmail == null) return;

                selectedEmail.EmailLogs.Add(new EmailLog()
                {
                    Action = action,
                    Email = selectedEmail,
                    TimeActionTaken = DateTime.Now.ToLocalTime(),
                    TakenBy = actionTakenBy
                });

                await ctx.SaveChangesAsync();
            }
        }

        public async Task LogEmailSeen(IMessageSummary message)
        {
            using (var Context = new MailModelContainer())
            {

                var selectedEmail = Context.Emails.Where(x => x.ImapMailBoxConfigurationId == _config.Id);
                var result = selectedEmail.FirstOrDefault(x => x.EnvelopeID.Equals(message.Envelope.MessageId));

                if (result == null || result.MarkedAsRead) return;

                result.MarkedAsRead = true;
                await Context.SaveChangesAsync();
                await LogEmailChanged(message, "Unknown", "Marked Read");
            }
        }

        public async Task LogEmailSent(MimeMessage message, string emailDestination, bool moved)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedEmail =
                    ctx.Emails.FirstOrDefault(x => x.EnvelopeID.Equals(message.MessageId) &&
                            x.ImapMailBoxConfiguration.Id == _config.Id);

                var mailBoxName = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == _config.Id);

                if (selectedEmail == null) return;

                var newLogs = new List<EmailLog>();

                try
                {
                    selectedEmail.BodyText =
                        HtmlToText.ConvertHtml(message.BodyParts.OfType<TextPart>().FirstOrDefault()?.Text);
                }
                catch (Exception ex)
                {
                    selectedEmail.BodyText = "-";
                }

                var log = new EmailLog()
                {
                    Action = moved ? $"Sent to {emailDestination} and moved to {mailBoxName?.MailBoxName}/{emailDestination}" : $"Sent to {emailDestination}",
                    Email = selectedEmail, TakenBy = emailDestination, TimeActionTaken = DateTime.Now.ToLocalTime()
                };

                selectedEmail.Minutes = (int)(DateTime.Now.ToUniversalTime() - selectedEmail.TimeReceived.ToUniversalTime()).TotalMinutes;
                selectedEmail.InQueue = false;

                newLogs.Add(log);
                ctx.EmailLogs.AddRange(newLogs);
                await ctx.SaveChangesAsync();
            }
        }
    }
}