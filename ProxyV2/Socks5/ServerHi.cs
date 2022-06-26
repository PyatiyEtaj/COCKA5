using System.Collections.Generic;
using System.Linq;

namespace ProxyV2.Socks5
{
    public class ServerHi
    {
        public byte Version { get; set; }
        public byte ChosenAuthMethod { get; set; } = Socks5Consts.WrongAuthMethod;

        public ServerHi(IEnumerable<byte> availableMethods)
        {
            Version = Socks5Consts.Socks5;
            ChosenAuthMethod = availableMethods.FirstOrDefault();
        }

        public byte[] ToByteArray()
        {
            return new byte[] { Version, ChosenAuthMethod };
        }

        public override string ToString()
        {
            return $"ver {Version} / chosen {ChosenAuthMethod}";
        }
    }
}
