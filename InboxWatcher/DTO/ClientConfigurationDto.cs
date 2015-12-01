using System.Runtime.Serialization;

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
    }
}