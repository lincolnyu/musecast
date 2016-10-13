using System;
using System.Linq;
using System.Net;

namespace MuseCastLib
{
    public static class NetHelper
    {
        public static void ParseIpAddress(string address, out IPAddress ipAddress, out int? port)
        {
            var s1 = address.Split(':');
            string addressStr;
            port = null;
            if (s1.Length == 2)
            {
                addressStr = s1[0];
                var portStr = s1[1];
                int portv;
                if (int.TryParse(portStr, out portv)) port = portv;
            }
            else if (s1.Length == 1)
            {
                addressStr = address;
            }
            else
            {
                throw new ArgumentException("Wrong IP address format");
            }

            var s2 = addressStr.Split('.');
            if (s2.Length != 4)
            {
                throw new ArgumentException("Wrong IP address format");
            }
            var ipComponents = s2.Select(x => byte.Parse(x)).ToArray();
            ipAddress = new IPAddress(ipComponents);
        }

    }
}
