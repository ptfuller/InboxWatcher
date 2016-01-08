using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class EmailSender
    {
        private SmtpClient _smtpClient;
        private ImapClientDirector _director;
        private Timer _timer;

        public EmailSender(ImapClientDirector director)
        {
            _director = director;
            _smtpClient = director.GetSmtpClient();
            
            _timer = new Timer();
            _timer.Interval = 1000*60*3; //3 minutes
            _timer.Elapsed += (s, e) => KeepAlive();
            _timer.AutoReset = true;
            _timer.Start();
        }

        private async void KeepAlive()
        {
            if (!_smtpClient.IsConnected || !_smtpClient.IsAuthenticated)
            {
                _smtpClient = _director.GetSmtpClient();
                KeepAlive();
            }

            try
            {
                await _smtpClient.NoOpAsync();
            }
            catch (Exception ex)
            {
                _smtpClient = _director.GetSmtpClient();
                return;
            }
        }

        public bool IsConnected()
        {
            return _smtpClient.IsConnected;
        }

        public bool IsAuthenticated()
        {
            return _smtpClient.IsAuthenticated;
        }

        public async Task<bool> SendMail(MimeMessage message, uint uniqueId, string emailDestination, bool moveToDest)
        {
            try
            {
               
                    var client = _smtpClient;

                    var buildMessage = new MimeMessage();
                    buildMessage.From.Add(new MailboxAddress(_director.SendAs, _director.UserName));
                    buildMessage.To.Add(new MailboxAddress(emailDestination, emailDestination));

                    buildMessage.ReplyTo.AddRange(message.From.Mailboxes);
                    buildMessage.ReplyTo.AddRange(message.Cc.Mailboxes);
                    buildMessage.From.AddRange(message.From.Mailboxes);
                    buildMessage.From.AddRange(message.Cc.Mailboxes);
                    buildMessage.Subject = message.Subject;

                    var builder = new BodyBuilder();

                    foreach (
                        var bodyPart in
                            message.BodyParts.Where(bodyPart => !bodyPart.ContentType.MediaType.Equals("text")))
                    {
                        builder.LinkedResources.Add(bodyPart);
                    }

                    string addresses = message.From.Mailboxes.Aggregate("",
                        (current, address) => current + (address.Address + " "));

                    string ccAddresses = message.Cc.Mailboxes.Aggregate("", (a, b) => a + (b.Address + " "));

                    string toAddresses = message.To.Mailboxes.Aggregate("", (a, b) => a + (b.Address + " "));

                    if (message.TextBody != null)
                    {
                        builder.TextBody = "***Message From " + _director.MailBoxName + "*** \nSent from: " + addresses +
                                           "\nSent to: " + toAddresses +
                                           "\nCC'd on email: " + ccAddresses + "\nMessage Date: " +
                                           message.Date.ToLocalTime().ToString("F")
                                           + "\n---\n\n" + message.TextBody;
                    }

                    if (message.HtmlBody != null)
                    {
                        builder.HtmlBody = "<p>***Message From " + _director.MailBoxName + "***<br/><p>Sent from: " +
                                           addresses +
                                           "<br/><p>Sent to: " + toAddresses +
                                           "<br/><p>CC'd on email: " + ccAddresses + "<br/><p>Message Date:" +
                                           message.Date.ToLocalTime().ToString("F") +
                                           "<br/>---<br/><br/>" + message.HtmlBody;
                    }

                    buildMessage.Body = builder.ToMessageBody();



                    await client.SendAsync(buildMessage);
                
            }
            catch (Exception ex)
            {
                if (ex is ServiceNotConnectedException || ex.InnerException is ServiceNotConnectedException)
                {
                    KeepAlive();
                    SendMail(message, uniqueId, emailDestination, moveToDest);
                }

                
                //send an email with error message
                return false;
            }

            return true;
        }
    }
}