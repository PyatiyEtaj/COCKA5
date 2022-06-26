using ProxyV2.Models;
using System;
using System.Net;
using System.Text;

namespace ProxyV2.Parsers
{
    public class HostDataParser : IParser
    {
        public string FindHost(StringBuilder str)
        {
            string lookingFor = "ost:";
            StringBuilder host = new StringBuilder();
            bool found = false;
            for (int i = 0; i < str.Length - 1 && !found; i++)
            {
                if (str[i] == 'h' || str[i] == 'H')
                {
                    found = true;
                    for (int j = i + 1, k = 0; j < str.Length && k < lookingFor.Length; j++, k++)
                    {
                        if (str[j] != lookingFor[k])
                            found = false;
                    }
                    if (found)
                    {
                        i += (lookingFor.Length + 1);

                        for (int j = i; j < str.Length; j++)
                        {
                            if (str[j] == '\n')
                            {
                                break;
                            }
                            host.Append(str[j]);
                        }
                    }
                }
            }
            return host.ToString().Trim();
        }

        public object Parse(string str)
        {
            var host = FindHost(new StringBuilder(str));
            if (host.Length > 0)
            {
                int port;
                var splitted = host.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length < 2 || !int.TryParse(splitted[1], out port))
                {
                    port = 80;
                }

                return new HostData
                {
                    IPHostEntry = Dns.GetHostEntry(splitted[0]),
                    Host = splitted[0],
                    Port = port,
                    OriginalData = str
                };
            }
            return new HostData
            {
                OriginalData = str
            };
        }
    }
}
