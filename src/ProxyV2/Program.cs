using Microsoft.Extensions.Logging;

namespace ProxyV2
{
    class Program
    {
        static void Main(string[] args)
        {
            var loggerfactory = LoggerFactory.Create(
                (buidler) => buidler.AddSystemdConsole());
            var logger = loggerfactory.CreateLogger<ServerSocks5>();
            string address = "127.0.0.1";
            int port = 8082;
            using (var server = new ServerSocks5(logger, 65536, 400))
            {
                server.Listen(address, port);
            }
        }
    }
}
