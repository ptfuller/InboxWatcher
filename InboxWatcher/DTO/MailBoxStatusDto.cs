using System;
using System.Collections.Generic;

namespace InboxWatcher.DTO
{
    public class MailBoxStatusDto
    {
        public IEnumerable<Exception> Exceptions { get; set; }
        public string StartTime { get; set; }
        public bool IdlerConnected { get; set; }
        public bool WorkerConnected { get; set; }
        public bool IdlerIdle { get; set; }
        public bool WorkerIdle { get; set; }
        public bool Green { get; set; }
        public bool SmtpConnected { get; set; }
        public bool SmtpAuthenticated { get; set; }
    }
}