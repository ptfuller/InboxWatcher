using System.Runtime.Serialization;
using InboxWatcher.Interface;

namespace InboxWatcher.DTO
{
    //[KnownType(typeof(ClientConfigurationDto))]
    public class ClientConfigurationDto : IClientConfiguration
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public bool UseSecure { get; set; }
        public string MailBoxName { get; set; }
        public int Id { get; set; }
        public string SmtpUserName { get; set; }
        public string SmtpPassword { get; set; }
        public string SmtpHostName { get; set; }
        public int SmtpPort { get; set; }

        public ImapMailBoxConfiguration GetMailBoxConfiguration()
        {
            var config = new ImapMailBoxConfiguration()
            {
                HostName = HostName,
                Id = Id,
                MailBoxName = MailBoxName,
                Password = Password,
                Port = Port,
                UseSecure = UseSecure,
                UserName = UserName,
                SmtpUserName = SmtpUserName,
                SmtpPassword = SmtpPassword,
                SmtpHostName = SmtpHostName,
                SmtpPort = SmtpPort
        };

            return config;
        }

        public ClientConfigurationDto()
        {
            
        }

        public ClientConfigurationDto(IClientConfiguration conf)
        {
            UserName = conf.UserName;
            Password = conf.Password;
            HostName = conf.HostName;
            Port = conf.Port;
            UseSecure = conf.UseSecure;
            MailBoxName = conf.MailBoxName;
            Id = conf.Id;
            SmtpUserName = conf.SmtpUserName;
            SmtpPassword = conf.SmtpPassword;
            SmtpHostName = conf.SmtpHostName;
            SmtpPort = conf.SmtpPort;
        }
    }
}