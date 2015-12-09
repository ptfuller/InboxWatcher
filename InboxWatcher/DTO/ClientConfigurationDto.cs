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
                UserName = UserName
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
        }
    }
}