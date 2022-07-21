using Microsoft.Extensions.Logging;
using ProxyV2.Socks5;
using System;
using System.Collections.Generic;
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

        private async ValueTask<IEnumerable<byte>> ReadAsync(Socket socket)
        {
            List<byte> data = new List<byte>();
            var buffer = new Memory<byte>(new byte[_config.BufferSizeBytes]);
            int readed = 0;
            do
            {
                readed = await socket.ReceiveAsync(buffer, SocketFlags.None);
                if (readed < 1) continue;

                if (readed >= _config.BufferSizeBytes)
                {
                    data.AddRange(buffer.ToArray());
                }
                else
                {
                    data.AddRange(buffer.Slice(0, readed).ToArray());
                }
            } while (socket.Available > 0);
            return data;
        }

        private async ValueTask WriteAsync(Socket socket, byte[] buffer)
        {
            var readOnlyBuffer = new ReadOnlyMemory<byte>(buffer);
            await socket.SendAsync(readOnlyBuffer, SocketFlags.None);
        }

        private async ValueTask<bool> TransferFromTo(Socket client, Socket server, byte[] buffer)
        {
            bool status = false;
            while (client.Available > 0)
            {
                var readed = await client.ReceiveAsync(new Memory<byte>(buffer), SocketFlags.None);
                if (readed > 0)
                {
                    var sended = await server.SendAsync(
                        new ReadOnlyMemory<byte>(buffer, 0, readed),
                        SocketFlags.None);
                }
                status = true;
            }
            return status;
        }

        private async ValueTask SocksTunnel(Socket client, Socket server)
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

        private async ValueTask TunnelingData(Socket client, Socket server)
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
            ValueTask<(Socket Socket, Socks5ServerResponseStatus Status)>
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

        private async ValueTask<Socket> ConnectionAsync(Socket socket)
        {
            var data = await ReadAsync(socket);
            var hi = new Socks5.ClientHi(data);

            Socks5.Socks5Validator.CheckVersion(hi.Version);

            var serverHi = new Socks5.ServerHi(hi.Methods);
            await WriteAsync(socket, serverHi.ToByteArray());

            data = await ReadAsync(socket);
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

            await WriteAsync(socket, resultSocks5.ToByteArray());

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
                        var server = await ConnectionAsync(stream.Socket);
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
