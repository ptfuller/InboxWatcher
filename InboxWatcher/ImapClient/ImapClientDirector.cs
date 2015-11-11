namespace InboxWatcher
{
    public class ImapClientDirector
    {
        private ImapClientBuilder Builder { get; set; }

        public ImapClientDirector(IClientConfiguration configuration)
        {
            Builder = new ImapClientBuilder()
                .WithHost(configuration.HostName)
                .WithPassword(configuration.Password)
                .WithPort(configuration.Port)
                .WithUseSecure(configuration.UseSecure)
                .WithUserName(configuration.UserName);
        }

        public virtual IImapClient GetClient()
        {
            return Builder.Build();
        }

        public virtual IImapClient GetReadyClient()
        {
            return Builder.BuildReady();
        }

        public virtual IImapClient GetThisClientReady(IImapClient client)
        {
            return Builder.GetReady(client);
        }
    }
}