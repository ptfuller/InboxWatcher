using System;
using System.Collections.Generic;
using System.Threading;
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

        /// <summary>
        /// Get a token that cancels after 1 minute
        /// </summary>
        /// <param name="ms">milleseconds to wait before cancel - default 60000</param>
        /// <returns></returns>
        public static CancellationToken GetCancellationToken(int ms = (1000 * 60 * 1))
        {
            var token = new CancellationTokenSource(ms);
            return token.Token;
        }
    }
}