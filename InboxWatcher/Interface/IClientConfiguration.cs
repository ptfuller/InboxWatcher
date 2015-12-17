namespace InboxWatcher.Interface
{
    public interface IClientConfiguration
    {
        string UserName { get; set; } 
        string Password { get; set; }
        string HostName { get; set; }
        int Port { get; set; }
        bool UseSecure { get; set; }
        string MailBoxName { get; set; }
        int Id { get; set; }
        string SmtpUserName { get; set; }
        string SmtpPassword { get; set; }
        string SmtpHostName { get; set; }
        int SmtpPort { get; set; }
    }
}