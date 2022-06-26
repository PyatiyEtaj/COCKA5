using ProxyV2.Models;
using ProxyV2.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ProxyV2
{
    public class Server : IDisposable
    {
        private byte[] _serverBuffer;
        private int _timeout = 1000;
        private TcpListener _server;
        private IParser _hostDataParser = new HostDataParser();
        public Server(int bufferSize, int timeout)
        {
            _serverBuffer = new byte[bufferSize];
            _timeout = timeout;
        }

        public void Dispose()
        {
            if (_server is not null)
            {
                _server.Stop();
            }
        }

        private async Task WaitUntilDataAvailableAsync(NetworkStream stream)
        {
            int cur = 0;
            while (!stream.DataAvailable && cur < _timeout)
            {
                await Task.Delay(100);
                cur += 100;
            }
        }

        private async Task<HostData> ReadAsync(Stream clientStream)
        {
            NetworkStream stream = clientStream as NetworkStream;
            List<byte> data = new List<byte>();

            int i = 1;
            if (stream is not null)
            {
                await WaitUntilDataAvailableAsync(stream);

                while (stream.DataAvailable)
                {
                    i = await stream.ReadAsync(_serverBuffer, 0, _serverBuffer.Length);
                    for (int j = 0; j < i; j++)
                    {
                        data.Add(_serverBuffer[j]);
                    }
                }
            }

            string str = Encoding.ASCII.GetString(data.ToArray());
            var color = Console.ForegroundColor;
            Console.WriteLine("\tGET:");
            Util.WriteLine(str.Split("\n\r\n")[0], ConsoleColor.Green);
            var host = (HostData)_hostDataParser.Parse(str);
            host.Bytes = data.ToArray();
            return host;
        }

        private async Task<HostData> WriteToClientAsync(Stream stream, HostData data, bool waitForAnswer)
        {
            var color = Console.ForegroundColor;
            Console.WriteLine("\tWRITE:");
            Util.WriteLine(data.OriginalData.Split("\n\r\n")[0], ConsoleColor.Green);
            await stream.WriteAsync(data.Bytes, 0, data.Bytes.Length);
            if (waitForAnswer)
            {
                var dataAfterSend = await ReadAsync(stream);
                return dataAfterSend;
            }
            return null;
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private async Task<HostData> WriteAsync(HostData data)
        {
            using (var client = new TcpClient(data.Host, data.Port))
            {
                using (var ssl = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    ssl.AuthenticateAsClient(data.Host);
                    // TODO SSL READING
                    return await WriteToClientAsync(ssl, data, true);
                }
            }    
        }

        public async Task Listen(string address, int port)
        {
            _server = new TcpListener(IPAddress.Parse(address), port);
            _server.Start();

            while (true)
            {
                Console.WriteLine("Waiting for a connection... ");
                TcpClient client = _server.AcceptTcpClient();
                try
                {
                    Console.WriteLine($"  + Connected: {client.Client.LocalEndPoint}/{client.Client.RemoteEndPoint}");
                    var stream = client.GetStream();
                    var data = await ReadAsync(stream);
                    if (data.Host is not null)
                    {
                        var sendedData = await WriteAsync(data);
                        if (sendedData.OriginalData is not null)
                        {
                            await WriteToClientAsync(stream, sendedData, false);
                        }                        
                    }                        
                }
                catch (Exception ex)
                {
                    Util.WriteLine(ex.Message, ConsoleColor.Red);
                }
                finally
                {
                    client.Close();
                }
            }
        }
    }
}
