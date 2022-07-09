using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProxyV2.Socks5
{
    public class ClientAfterHi
    {
        public byte Version { get; set; }
        public Socks5.Command Command { get; set; }
        public byte Reserved { get; set; }
        public AddressType AdressType { get; set; }
        public byte[] Address { get; set; }
        public byte[] PortBytes { get; set; }
        public short Port { get => (short)(PortBytes[0] << 8 | PortBytes[1]); }

        public ClientAfterHi(IEnumerable<byte> data)
        {
            if (data.Count() < 4) throw new Exception("wrong after hi socks5 bytes");

            Version = data.ElementAt(0);
            Command = (Socks5.Command)data.ElementAt(1);
            // Reserved = data[0x2];
            AdressType = (Socks5.AddressType)data.ElementAt(3);

            int skip = AdressType == AddressType.DomainName ? 5 : 4;

            Address = data.Skip(skip).Take(data.Count() - skip - 2).ToArray();
            PortBytes = data.Skip(skip + Address.Length).ToArray();
        }

        public override string ToString()
        {
            string addr = default;
            if (AdressType == AddressType.IPv4 || AdressType == AddressType.IPv6)
            {
                addr = string.Join('.', Address);
            }
            else if (AdressType == AddressType.DomainName)
            {
                addr = Encoding.ASCII.GetString(Address);
            }

            var protocol = Command == Command.AsociateUdp ? "Udp" : "Tcp";

            return $"type [{AdressType}] addr [{addr} {Port} {protocol}]";
        }
    }
}
