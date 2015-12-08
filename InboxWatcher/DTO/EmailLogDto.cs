using System;

namespace InboxWatcher.DTO
{
    public class EmailLogDto : IEmailLog
    {
        public int Id { get; set; }
        public string TakenBy { get; set; }
        public string Action { get; set; }
        public DateTime TimeActionTaken { get; set; }
        public int EmailId { get; set; }

        public EmailLogDto() { }

        public EmailLogDto(EmailLog el)
        {
            Id = el.Id;
            TakenBy = el.TakenBy;
            Action = el.Action;
            TimeActionTaken = el.TimeActionTaken;
            EmailId = el.EmailId;
        }
    }
}