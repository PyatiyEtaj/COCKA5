using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyV2.Socks5
{
    public class Socks5Validator
    {
        public static void CheckVersion(byte version)
        {
            if (version != Socks5Consts.Socks5) throw new Exception($"Wrong socks version must be {Socks5Consts.Socks5} but we get {version}");
        }
    }
}
