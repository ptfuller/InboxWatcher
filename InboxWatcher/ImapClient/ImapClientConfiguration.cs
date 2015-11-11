namespace InboxWatcher
{
    public class ImapClientConfiguration : IClientConfiguration
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public bool UseSecure { get; set; }
    }
}