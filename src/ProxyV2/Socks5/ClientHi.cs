using System;
using System.Collections.Generic;
using System.Linq;

namespace ProxyV2.Socks5
{
    public class ClientHi
    {
        public byte Version { get; set; }
        public byte CountOfSupportedAuthMethods { get; set; }
        public List<byte> Methods { get; set; }

        public ClientHi(IEnumerable<byte> data)
        {
            if (data.Count() < 3) throw new Exception("wrong client hi structure");

            Version = data.ElementAt(0);
            CountOfSupportedAuthMethods = data.ElementAt(1);
            Methods = new List<byte>(data.Skip(2));
        }

        public override string ToString()
        {
            return $"ver: {Version} / support auth methods: {CountOfSupportedAuthMethods} / methods:{Util.ByteArrayToString(Methods)}";
        }
    }
}
