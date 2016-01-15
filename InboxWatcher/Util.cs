using System;
using System.Collections.Generic;
using System.Linq;
using MimeKit;

namespace InboxWatcher
{
    public static class Util
    {
        public static Dictionary<string, string> InternetAddressListToDictionary(this InternetAddressList list)
        {
            var result = new Dictionary<string, string>();

            foreach (var address in list.Mailboxes)
            {
                try
                {
                    result.Add(address.Address, address.Name);
                }
                catch (ArgumentException ex)
                {
                    //don't duplicate an address
                }
            }

            return result;
        }
    }
}