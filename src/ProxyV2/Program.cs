using Microsoft.Extensions.Logging;

namespace ProxyV2
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = LoggerFactory
                .Create(buidler => buidler.AddSystemdConsole())
                .CreateLogger<ServerSocks5>();

            var config = new ServerConfiguration
            {
                Host = "127.0.0.1",
                Port = 8082,
                BufferSizeBytes = 65536,
                CountOfTriesReadDataFromSocket = 10,
                TimeoutBetweenReadWriteSocketDataMs = 100,
                ReconnectMaxTries = 3,
                ReconnectTimeoutMs = 1000,
            };

            using (var server = new ServerSocks5(logger, config))
            {
                server.Listen();
            }
        }
    }
}
