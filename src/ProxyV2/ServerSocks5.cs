﻿using Microsoft.Extensions.Logging;
using ProxyV2.Socks5;
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
using System.Threading;
using System.Threading.Tasks;

namespace ProxyV2
{
    public class ServerSocks5 : IDisposable
    {
        private readonly ILogger<ServerSocks5> _logger;
        private readonly int _bufferSize;
        private readonly int _timeout = 1000;

        private TcpListener _server;
        public ServerSocks5(ILogger<ServerSocks5> logger, int bufferSize, int timeout)
        {
            _logger = logger;
            _bufferSize = bufferSize;
            _timeout = timeout;
        }

        public void Dispose()
        {
            if (_server is not null)
            {
                _server.Stop();
            }
        }

        private async Task<List<byte>> ReadAsync(Stream stream)
        {
            List<byte> data = new List<byte>();
            var buffer = new byte[_bufferSize];
            stream.ReadTimeout = _timeout;
            int read;
            do
            {
                read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    data.AddRange(buffer.Take(read));
                }
            }
            while (read >= buffer.Length);

            return data;
        }

        private async Task WriteAsync(Stream stream, byte[] buffer)
        {
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private SocketFlags GetSocketFlag(bool isNeedToBePartial)
            => isNeedToBePartial ? SocketFlags.ControlDataTruncated : SocketFlags.None;

        private async Task<bool> TransferFromTo(Socket client, Socket server, byte[] buffer)
        {
            bool status = false;
            while (client.Available > 0)
            {
                var readed = await client.ReceiveAsync(buffer, SocketFlags.None);
                if (readed > 0)
                {
                    var sended = await server.SendAsync(
                        new ArraySegment<byte>(buffer, 0, readed), 
                        GetSocketFlag(readed >= buffer.Length));
                }
                status = true;
            }
            return status;
        }

        private async Task SocksTunnel(Socket client, Socket server)
        {
            var buffer = new byte[_bufferSize];
            int countOfTry = 0;
            do
            {
                var status = await TransferFromTo(client, server, buffer) |
                             await TransferFromTo(server, client, buffer);

                countOfTry++;
                if (status) 
                    countOfTry = 0;

                await Task.Delay(100);
            } while (countOfTry < 10);
        }

        private async Task TunnelingData(Socket client, ConnectionInfo info)
        {
            using (var server = new Socket(SocketType.Stream, info.ProtocolType))
            {
                await server.ConnectAsync(info.Addr, info.Port, CancellationToken.None);
                await SocksTunnel(client, server);
                _logger.LogInformation($"Tunnel is close -- client:{client.RemoteEndPoint} remote:{info.Addr}");
            }
        }

        private async Task<ConnectionInfo> ConnectionAsync(Stream stream)
        {
            var data = await ReadAsync(stream);
            var hi = new Socks5.ClientHi(data);

            Socks5.Socks5Validator.CheckVersion(hi.Version);

            var serverHi = new Socks5.ServerHi(hi.Methods);
            await WriteAsync(stream, serverHi.ToByteArray());

            data = await ReadAsync(stream);
            var afterHi = new Socks5.ClientAfterHi(data);
            _logger.LogInformation($"Get connected - {afterHi}");
            var resolved = Socks5.AddressResolver.Resolve(afterHi.AdressType, afterHi.Address);
            if (resolved.AdressType == Socks5.AddressType.Error) throw new Exception("Cant Dns.GetHostName");

            var resultSocks5 = new Socks5.ServerAfterHi
            {
                Status = Socks5.Socks5ServerResponseStatus.Ok,
                Address = resolved.Address,
                PortBytes = afterHi.Port,
                AdressType = resolved.AdressType
            };

            await WriteAsync(stream, resultSocks5.ToByteArray());
            
            return new ConnectionInfo
            {
                Addr = new IPAddress(resultSocks5.Address),
                Port = resultSocks5.Port,
                ProtocolType = afterHi.Command == Command.AsociateUdp 
                    ? ProtocolType.Udp 
                    : ProtocolType.Tcp
            };
        }

        public void Listen(string address, int port)
        {
            _server = new TcpListener(IPAddress.Parse(address), port);
            _server.Start();

            _logger.LogInformation($"Listen {address}:{port}");
            while (true)
            {
                var client = _server.AcceptTcpClient();
                Task.Run(async () => {
                    var localClient = client;
                    try
                    {
                        _logger.LogInformation($"Connected: {client.Client.LocalEndPoint}/{client.Client.RemoteEndPoint}");
                        var stream = client.GetStream();
                        var connectionData = await ConnectionAsync(stream);
                        _logger.LogInformation("Socks5 Connection success");

                        await TunnelingData(client.Client, connectionData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }
                    finally
                    {
                        localClient.Close();
                    }
                });
            }
        }
    }
}
