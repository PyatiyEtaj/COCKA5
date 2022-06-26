using ProxyV2.Models;
using ProxyV2.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ProxyV2
{
    public class ServerSocks5 : IDisposable
    {
        private byte[] _serverBuffer;
        private int _timeout = 1000;
        private TcpListener _server;
        private IParser _hostDataParser = new HostDataParser();
        public ServerSocks5(int bufferSize, int timeout)
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
        
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
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

        private async Task<List<byte>> ReadAsync(Stream clientStream)
        {
            NetworkStream stream = clientStream as NetworkStream;
            List<byte> data = new List<byte>();

            if (stream is not null)
            {
                await WaitUntilDataAvailableAsync(stream);

                int i;
                while (stream.DataAvailable)
                {
                    i = await stream.ReadAsync(_serverBuffer, 0, _serverBuffer.Length);
                    for (int j = 0; j < i; j++)
                    {
                        data.Add(_serverBuffer[j]);
                    }
                }
            }

            return data;
        }

        private Task WriteAsync(Stream stream, byte[] buffer)
        {
            return stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private async Task<List<byte>> SslRequestAsync(byte[] addr, string host, byte[] data)
        {
            using (var client = new TcpClient(AddressFamily.InterNetwork))
            {
                client.Connect(new IPEndPoint(new IPAddress(addr), 443));
                using (var ssl = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    ssl.AuthenticateAsClient(host);
                    await WriteAsync(ssl, data);
                    return await ReadAsync(ssl);
                }
            }
        }
        private async Task<List<byte>> RequestAsync(IPAddress info, int port, byte[] data)
        {
            using (var client = new TcpClient(AddressFamily.InterNetwork))
            {
                client.Connect(info, port);
                var stream = client.GetStream();
                await WriteAsync(stream, data);
                return await ReadAsync(stream);
            }
        }

        private async Task ConnectionAsync(Stream stream)
        {
            var data = await ReadAsync(stream);
            var hi = new Socks5.ClientHi(data);

            Socks5.Socks5Validator.CheckVersion(hi.Version);

            var serverHi = new Socks5.ServerHi(hi.Methods);
            await WriteAsync(stream, serverHi.ToByteArray());

            data = await ReadAsync(stream);
            var afterHi = new Socks5.ClientAfterHi(data);
            Util.WriteLine($"\t\tget connected - {afterHi}", ConsoleColor.Green);
            var resolved = Socks5.AddressResolver.Resolve(afterHi.AdressType, afterHi.Address);
            if (resolved.AdressType == Socks5.AddressType.Error) throw new Exception("Cant Dns.GetHostName");

            var resultSocks5 = new Socks5.ServerAfterHi(data)
            {
                Status = Socks5.Socks5ServerResponseStatus.Ok,
                AdressType = resolved.AdressType,
                Address = resolved.Address
            };
            await WriteAsync(stream, resultSocks5.ToByteArray());
            Util.WriteLine($"\t\tConnection success", ConsoleColor.Green);

            data = await ReadAsync(stream);
            Util.WriteLine($"\t\tGetting {data.Count} bytes", ConsoleColor.White);
            if (resultSocks5.Port == 443)
            {
                data = await SslRequestAsync(
                    resultSocks5.Address,
                    Socks5.AddressResolver.GetHostName(afterHi.AdressType, afterHi.Address), 
                    data.ToArray());
            } 
            else
            {
                data = await RequestAsync(
                    new IPAddress(resultSocks5.Address),
                    resultSocks5.Port,
                    data.ToArray());
            }

            Util.WriteLine($"\t\tget after resend {data.Count}", ConsoleColor.White);
            await WriteAsync(stream, data.ToArray());
            //Util.WriteLine($"\t\tGetting RAW:{Util.ByteArrayToString(data)}\n\t\t{Encoding.ASCII.GetString(data.ToArray())}", ConsoleColor.Green);
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
                    await ConnectionAsync(stream);
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
