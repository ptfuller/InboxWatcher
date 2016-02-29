﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;
using NLog;
using Timer = System.Timers.Timer;

namespace InboxWatcher.ImapClient
{
    public class EmailSender : IDisposable
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private SmtpClient _smtpClient;
        private IClientConfiguration _configuration;
        private Timer _timer;
        private Task _recoveryTask;
        private bool _emailIsSending;
        private bool _setupInProgress;
        private SemaphoreSlim _setupSemaphore = new SemaphoreSlim(1);

        public event EventHandler<InboxWatcherArgs> ExceptionHappened;

        public EmailSender(IClientConfiguration configuration)
        {
            _configuration = configuration;
        }

        private async void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await KeepAlive();
        }

        public async Task Setup()
        {
            try
            {
                if (_setupInProgress) return;
                await _setupSemaphore.WaitAsync(Util.GetCancellationToken(10000));
                _setupInProgress = true;

                if (_timer != null)
                {
                    _timer.Enabled = false;
                    _timer.Elapsed -= _timer_Elapsed;
                    _timer.Dispose();
                }

                _timer = new Timer();
                _timer.Interval = 1000 * 60 * 2; //2 minutes
                _timer.Elapsed += _timer_Elapsed;
                _timer.AutoReset = false;
                _timer.Start();

                SmtpClient oldClient = null;

                if (_smtpClient != null)
                {
                    _smtpClient.Disconnected -= SmtpClientOnDisconnected;
                    oldClient = _smtpClient;
                }

                //_smtpClient = await _configuration.GetSmtpClient();

                oldClient?.Dispose();

                _smtpClient.Disconnected += SmtpClientOnDisconnected;

                //Trace.WriteLine($"{_configuration.MailBoxName}: SMTP Client Setup");

                _setupSemaphore.Release();
                _setupInProgress = false;
            }
            catch (Exception ex)
            {
                //Trace.WriteLine($"{_configuration.MailBoxName}:{ex.Message}");
            }
            
        }

        private async void SmtpClientOnDisconnected(object sender, EventArgs eventArgs)
        {
            //Trace.WriteLine($"{_configuration.MailBoxName}: SMTP Client Disconnected");
            await Setup();
        }

        private async Task KeepAlive()
        {
            if (_emailIsSending) return;

            try
            {
                await _smtpClient.NoOpAsync(Util.GetCancellationToken(1000));
                //Trace.WriteLine($"{_configuration.MailBoxName}:SMTP Client NoOp successful");
                _timer.Start();
            }
            catch (Exception ex)
            {
                var exception = new Exception("Exception happened during SMTP client No Op", ex);
                //Trace.WriteLine($"{_configuration.MailBoxName}:SMTP Client NoOp failed{exception.InnerException}");

                await Setup();
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

        public async Task<bool> SendMail(MimeMessage message, string emailDestination, bool moveToDest)
        {
            if (_setupInProgress) return false;
            _emailIsSending = true;
            _timer.Stop();

            try
            {
                var client = _smtpClient;

                var buildMessage = new MimeMessage();
                buildMessage.Sender = new MailboxAddress(_configuration.MailBoxName, _configuration.UserName);
                buildMessage.From.Add(new MailboxAddress(_configuration.MailBoxName, _configuration.UserName));
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
                    (current, address) => current + (address.Address + "; "));

                string ccAddresses = message.Cc.Mailboxes.Aggregate("", (a, b) => a + (b.Address + "; "));

                string toAddresses = message.To.Mailboxes.Aggregate("", (a, b) => a + (b.Address + "; "));

                if (message.TextBody != null)
                {
                    builder.TextBody = "***Message From " + _configuration.MailBoxName + "*** \n" +
                                       "Message_pulled_by: " + emailDestination +
                                       "\nSent from: " + addresses +
                                       "\nSent to: " + toAddresses +
                                       "\nCC'd on email: " + ccAddresses + "\nMessage Date: " +
                                       message.Date.ToLocalTime().ToString("F")
                                       + "\n---\n" + message.TextBody;
                }

                if (message.HtmlBody != null)
                {
                    builder.HtmlBody = "***Message From " + _configuration.MailBoxName + "*** <br/>" +
                                       "Message_pulled_by: " + emailDestination +
                                       "<br/>Sent from: " + addresses + "<br/>Sent to: " + toAddresses +
                                       "<br/>CC'd on email: " + ccAddresses + "<br/>Message Date:" +
                                       message.Date.ToLocalTime().ToString("F") +
                                       "<br/>---<br/>" + message.HtmlBody;
                }

                buildMessage.Body = builder.ToMessageBody();

                await client.SendAsync(buildMessage);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);

                await Setup();
                
                _emailIsSending = false;
                _timer.Start();

                return false;
            }

            _emailIsSending = false;
            _timer.Start();
            return true;
        }

        public void Dispose()
        {
            _smtpClient.Dispose();
            _timer.Dispose();
        }
    }
}