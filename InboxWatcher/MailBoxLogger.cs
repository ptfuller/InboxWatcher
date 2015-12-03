using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using MailKit;

namespace InboxWatcher
{
    public static class MailBoxLogger
    {

        public static void LogEmailReceived(IMessageSummary summary, IClientConfiguration config)
        {
            using (var Context = new MailModelContainer())
            {
                //if it's already in the DB we're not going to log it received
                if (Context.Emails.Any(x => x.EnvelopeID.Equals(summary.Envelope.MessageId))) return;

                //generate new Email and EmailLogs
                var email = new Email()
                {
                    BodyText = "",
                    EnvelopeID = summary.Envelope.MessageId,
                    InQueue = true,
                    MarkedAsRead = false,
                    Minutes = (int) (DateTime.Now.ToLocalTime() - summary.Date.LocalDateTime).TotalMinutes,
                    Sender = summary.Envelope.From.ToString(),
                    Subject = summary.Envelope.Subject,
                    TimeReceived = summary.Date.LocalDateTime,
                    ImapMailBoxConfigurationId = config.Id
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
                        Console.WriteLine(
                            "Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                            eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                ve.PropertyName, ve.ErrorMessage);
                        }
                    }

                    throw ex;
                }
            }
        }

        public static void LogEmailRemoved(IMessageSummary email, IClientConfiguration config)
        {
            using (var Context = new MailModelContainer())
            {

                LogEmailChanged(email, config, "", "Removed");
                var selectedEmails =
                    Context.Emails.Where(
                        x => config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(email.Envelope.MessageId));

                foreach (var em in selectedEmails)
                {
                    em.Minutes = (int) (DateTime.Now.ToLocalTime() - em.TimeReceived.ToLocalTime()).TotalMinutes;
                    em.InQueue = false;
                }

                Context.SaveChanges();
            }
        }

        public static void LogEmailChanged(IMessageSummary email, IClientConfiguration config, string actionTakenBy, string action)
        {
            using (var Context = new MailModelContainer())
            {

                var selectedEmails =
                    Context.Emails.Where(
                        x => config.Id == x.ImapMailBoxConfigurationId && x.EnvelopeID.Equals(email.Envelope.MessageId));

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

        public static void LogEmailSeen(IClientConfiguration config, IMessageSummary message)
        {
            using (var Context = new MailModelContainer())
            {

                var selectedEmail = Context.Emails.Where(x => x.ImapMailBoxConfigurationId == config.Id);
                var result = selectedEmail.First(x => x.EnvelopeID.Equals(message.Envelope.MessageId));
                result.MarkedAsRead = true;
                LogEmailChanged(message, config, "Unknown", "Marked Read");
            }
        }
    }
}