namespace InboxWatcher.DTO
{
    public class EmailFilterDto
    {
        public int Id { get; set; }
        public string FilterName { get; set; }
        public string SubjectContains { get; set; }
        public string SentFromContains { get; set; }
        public string ForwardToAddress { get; set; }
        public bool ForwardThis { get; set; }
        public string MoveToFolder { get; set; }
    }
}