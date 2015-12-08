namespace InboxWatcher
{
    public interface IEmailLog
    {
        int Id { get; set; }
        string TakenBy { get; set; }
        string Action { get; set; }
        System.DateTime TimeActionTaken { get; set; }
        int EmailId { get; set; }
    }
}