using System.Collections.Generic;
using System.Linq;
using MimeKit;

namespace InboxWatcher
{
    public static class Util
    {
        public static Dictionary<string, string> InternetAddressListToDictionary(this InternetAddressList list)
        {
            return list.Mailboxes.ToDictionary(address => address.Address, address => address.Name);
        }
    }
}