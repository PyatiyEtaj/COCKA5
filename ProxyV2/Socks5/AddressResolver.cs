using System.Linq;
using System.Net;
using System.Text;

namespace ProxyV2.Socks5
{
    public class AddressResolver
    {
        public static (AddressType AdressType, byte[] Address) Resolve(AddressType type, byte[] address)
        {
            if (type == Socks5.AddressType.DomainName)
            {
                var info = Dns.GetHostEntry(Encoding.ASCII.GetString(address));
                if (info?.AddressList.Length > 0)
                {
                    var adr = info.AddressList.FirstOrDefault();
                    var adrType = adr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        ? AddressType.IPv4
                        : AddressType.IPv6;
                    return (adrType, adr.GetAddressBytes());
                }
                return (AddressType.Error, null);
            }

            return (type, address);
        }

        public static string GetHostName(AddressType type, byte[] address)
        {
            return type == AddressType.DomainName ? Encoding.ASCII.GetString(address) : "";
        }

    }
}
