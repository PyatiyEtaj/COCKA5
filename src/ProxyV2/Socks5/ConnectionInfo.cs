using System.Net;
using System.Net.Sockets;

namespace ProxyV2.Socks5
{
    public class ConnectionInfo
    {
        public IPAddress Addr { get; init; }
        public int Port { get; init; }
        public ProtocolType ProtocolType { get; init; }
    }
}
