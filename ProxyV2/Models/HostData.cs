using System.Net;

namespace ProxyV2.Models
{
    public class HostData
    {
        public IPHostEntry IPHostEntry { get; set; } = null;
        public string Host { get; set; } = null;

        public int Port { get; set; }
        public string OriginalData { get; set; } = null;
        public byte[] Bytes { get; set; } = null;
    }
}
