using System;
using System.Collections.Generic;
using System.Linq;

namespace ProxyV2.Socks5
{
    public class ServerAfterHi
    {
        public byte Version { get; }
        public Socks5ServerResponseStatus Status { get; set; }
        public byte Reserved { get; }
        public Socks5.AddressType AdressType { get; set; }
        public byte[] Address { get; set; }
        public short Port { get => (short)(_port[0] << 8 | _port[1]); }
        private byte[] _port;

        public ServerAfterHi(IEnumerable<byte> data)
        {
            if (data.Count() < 4) throw new Exception("wrong after hi socks5 bytes");

            Version = data.ElementAt(0);
            Status = (Socks5.Socks5ServerResponseStatus)data.ElementAt(1);
            // Reserved
            AdressType = (Socks5.AddressType)data.ElementAt(3);

            int skip = AdressType == AddressType.DomainName ? 5 : 4;

            Address = data.Skip(skip).Take(data.Count() - skip - 2).ToArray();
            _port = data.Skip(skip + Address.Length).ToArray();
        }

        public byte[] ToByteArray()
        {
            return new byte[] {
                Version,
                (byte)Status,
                Reserved,
                (byte)AdressType,
            }
            .Concat(Address)
            .Concat(_port)
            .ToArray();
        }
    }
}
