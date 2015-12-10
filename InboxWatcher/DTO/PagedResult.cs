using System.Collections.Generic;

namespace InboxWatcher.DTO
{
    public class PagedResult
    {
        public int total { get; set; }
        public IEnumerable<object> rows { get; set; }
    }
}