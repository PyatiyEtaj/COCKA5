using System;
using System.Collections.Generic;
using System.Linq;

namespace ProxyV2.Socks5
{
    public class ServerAfterHi
    {
        public byte Version { get; } = Socks5.Socks5Consts.Socks5;
        public Socks5ServerResponseStatus Status { get; set; }
        public byte Reserved { get; } = 0x00;
        public Socks5.AddressType AdressType { get; set; }
        public byte[] Address { get; set; }
        public short Port { get => (short)(PortBytes[0] << 8 | PortBytes[1]); }
        public byte[] PortBytes { get; set; } = new byte[2];

        public byte[] ToByteArray()
        {
            return new byte[] {
                Version,
                (byte)Status,
                Reserved,
                (byte)AdressType,
            }
            .Concat(Address)
            .Concat(PortBytes)
            .ToArray();
        }
    }
}
