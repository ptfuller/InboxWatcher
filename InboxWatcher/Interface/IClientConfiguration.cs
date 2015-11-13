namespace InboxWatcher
{
    public interface IClientConfiguration
    {
        string UserName { get; set; } 
        string Password { get; set; }
        string HostName { get; set; }
        int Port { get; set; }
        bool UseSecure { get; set; }
        string MailBoxName { get; set; }
    }
}