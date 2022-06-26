using System.Threading.Tasks;
using System;
using System.Net;

namespace ProxyV2
{
    class Program
    {

        static async Task Main(string[] args)
        {
            string address = "192.168.0.104";
            int port = 8082;
            Console.WriteLine($"==< start on {address}:{port} >==");
            await new ServerSocks5(256, 2000).Listen(address, 8082);
        }
    }
}
