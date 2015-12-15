using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
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

        public bool LogEmailReceived(IMessageSummary summary)
        {

            if (string.IsNullOrEmpty(summary.Envelope.MessageId)) return false;

            using (var Context = new MailModelContainer())
            {
                //if it's already in the DB we're not going to log it received
                if (Context.Emails.Any(x => x.EnvelopeID.Equals(summary.Envelope.MessageId))) return false;

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
                    Context.Emails.Add(email);
                    Context.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Debug.WriteLine(
                            "Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                            eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Debug.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                ve.PropertyName, ve.ErrorMessage);
                        }
                    }

                    throw ex;
                }
            }

            return true;
        }

        public void LogEmailRemoved(IMessageSummary email)
        {
            using (var Context = new MailModelContainer())
            {
                var selectedEmail =
                    Context.Emails.Include(e => e.EmailLogs).FirstOrDefault(
                        x => _config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(email.Envelope.MessageId));

                if (selectedEmail == null) return;

                if (selectedEmail.EmailLogs.Any(x => x.Action.Contains("Sent") && x.Action.Contains("moved")))
                {
                    return;
                }
                

                LogEmailChanged(email, "", "Removed");

                selectedEmail.Minutes = (int) (DateTime.Now.ToUniversalTime() - selectedEmail.TimeReceived.ToUniversalTime()).TotalMinutes;
                selectedEmail.InQueue = false;
                

                Context.SaveChanges();
            }
        }

        public void LogEmailChanged(IMessageSummary email, string actionTakenBy, string action)
        {
            using (var Context = new MailModelContainer())
            {

                var selectedEmails =
                    Context.Emails.Where(
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

                Context.EmailLogs.AddRange(newLogs);
                Context.SaveChanges();
            }
        }

        public void LogEmailSeen(IMessageSummary message)
        {
            using (var Context = new MailModelContainer())
            {

                var selectedEmail = Context.Emails.Where(x => x.ImapMailBoxConfigurationId == _config.Id);
                var result = selectedEmail.First(x => x.EnvelopeID.Equals(message.Envelope.MessageId));
                result.MarkedAsRead = true;
                Context.SaveChanges();
                LogEmailChanged(message, "Unknown", "Marked Read");
            }
        }

        public void LogEmailSent(MimeMessage message, string emailDestination, bool moved)
        {
            using (var ctx = new MailModelContainer())
            {
                var selectedEmail =
                    ctx.Emails.FirstOrDefault(x => x.EnvelopeID.Equals(message.MessageId) &&
                            x.ImapMailBoxConfiguration.Id == _config.Id);

                var mailBoxName = ctx.ImapMailBoxConfigurations.FirstOrDefault(x => x.Id == _config.Id);

                if (selectedEmail == null) return;

                var newLogs = new List<EmailLog>();

                selectedEmail.BodyText = HtmlToText.ConvertHtml(message.BodyParts.OfType<TextPart>().FirstOrDefault()?.Text);

                var log = new EmailLog()
                {
                    Action = moved ? $"Sent to {emailDestination} and moved to {mailBoxName?.MailBoxName}/{emailDestination}" : $"Sent to {emailDestination}",
                    Email = selectedEmail, TakenBy = emailDestination, TimeActionTaken = DateTime.Now
                };

                newLogs.Add(log);
                ctx.EmailLogs.AddRange(newLogs);
                ctx.SaveChanges();
            }
        }
    }
}