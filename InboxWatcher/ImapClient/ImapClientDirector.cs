namespace InboxWatcher
{
    public static class ImapClientDirector
    {
        static ImapClientDirector()
        {
            Builder = new ImapClientBuilder();

            //todo confiugre builder via config db
        }

        //todo make private
        public static ImapClientBuilder Builder { get; set; }

        public static IImapClient GetClient()
        {
            return Builder.Build();
        }

        public static IImapClient GetReadyClient()
        {
            return Builder.BuildReady();
        }

        public static IImapClient GetThisClientReady(IImapClient client)
        {
            return Builder.GetReady(client);
        }
    }
}