using Microsoft.Extensions.Logging;
using ProxyV2.Socks5;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyV2
{
    public class ServerSocks5 : IDisposable
    {
        private readonly ILogger<ServerSocks5> _logger;
        private readonly ServerConfiguration _config;

        private TcpListener _server;
        public ServerSocks5(ILogger<ServerSocks5> logger, ServerConfiguration config)
        {
            _logger = logger;
            _config = config;
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
            var buffer = new byte[_config.BufferSizeBytes];
            stream.ReadTimeout = _config.TimeoutBetweenReadWriteSocketDataMs *
                _config.CountOfTriesReadDataFromSocket;
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
            var buffer = new byte[_config.BufferSizeBytes];
            int countOfTry = 0;
            do
            {
                var status = await TransferFromTo(client, server, buffer) |
                             await TransferFromTo(server, client, buffer);

                countOfTry++;
                if (status)
                    countOfTry = 0;

                await Task.Delay(_config.TimeoutBetweenReadWriteSocketDataMs);
            } while (countOfTry < _config.CountOfTriesReadDataFromSocket);
        }

        private async Task TunnelingData(Socket client, Socket server)
        {
            try
            {
                await SocksTunnel(client, server);
                _logger.LogInformation($"Tunnel is close -- client:{client.RemoteEndPoint}/" +
                    $"remote:{server.RemoteEndPoint}");
            }
            finally
            {
                server.Disconnect(false);
                server.Dispose();
            }
        }

        private async
            Task<(Socket Socket, Socks5ServerResponseStatus Status)>
            TryToConnect(byte[] addr, short port, Command cmd)
        {
            var status = Socks5ServerResponseStatus.Ok;

            var socket = new Socket(
                SocketType.Stream,
                cmd == Command.AsociateUdp
                    ? ProtocolType.Udp
                    : ProtocolType.Tcp);

            try
            {
                int reconnectTry = 0;
                do
                {
                    await socket.ConnectAsync(
                        new IPAddress(addr),
                        port,
                        CancellationToken.None);
                    if (socket.Connected)
                    {
                        status = Socks5ServerResponseStatus.Ok;
                        break;
                    }
                    else
                    {
                        ++reconnectTry;
                        status = Socks5ServerResponseStatus.ConnectionFailure;
                        _logger.LogWarning($"Cant connect, try to reconnect [{reconnectTry}] " +
                            $"after {_config.ReconnectTimeoutMs}");
                        await Task.Delay(_config.ReconnectTimeoutMs);
                    }
                } while (reconnectTry < _config.ReconnectMaxTries);
            }
            catch (Exception ex)
            {
                status = Socks5ServerResponseStatus.NetUnavailable;
                socket.Disconnect(false);
                socket.Dispose();
            }

            return (socket, status);
        }

        private async Task<Socket> ConnectionAsync(Stream stream)
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
            if (resolved.AdressType == Socks5.AddressType.Error)
                throw new Exception("Cant Dns.GetHostName");

            var result = await TryToConnect(resolved.Address, afterHi.Port, afterHi.Command);

            var resultSocks5 = new Socks5.ServerAfterHi
            {
                Status = result.Status,
                Address = resolved.Address,
                PortBytes = afterHi.PortBytes,
                AdressType = resolved.AdressType
            };

            await WriteAsync(stream, resultSocks5.ToByteArray());

            return result.Socket;
        }

        public void Listen()
        {
            _server = new TcpListener(IPAddress.Parse(_config.Host), _config.Port);
            _server.Start();

            _logger.LogInformation($"Listen {_config.Host}:{_config.Port}");
            while (true)
            {
                var client = _server.AcceptTcpClient();
                Task.Run(async () =>
                {
                    var localClient = client;
                    try
                    {
                        _logger.LogInformation($"Connected -- local:{localClient.Client.LocalEndPoint}/" +
                            $"remote:{localClient.Client.RemoteEndPoint}");
                        var stream = localClient.GetStream();
                        var server = await ConnectionAsync(stream);
                        if (server?.Connected == true)
                        {
                            _logger.LogInformation("Socks5 Connection success");
                            await TunnelingData(localClient.Client, server);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }
                    finally
                    {
                        _logger.LogInformation($"Close -- local:{localClient.Client.LocalEndPoint}/" +
                            $"remote:{localClient.Client.RemoteEndPoint}");
                        localClient.Close();
                    }
                });
            }
        }
    }
}
