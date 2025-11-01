using System;
using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Server
{
    /// <summary>
    /// Accept TCP connections
    /// </summary>
    public class ConnectionHandler
    {
        /// <summary>
        /// Horse server of connection handler
        /// </summary>
        private readonly HorseServer _server;

        /// <summary>
        /// Host listener object of connection handler
        /// </summary>
        private readonly HostListener _listener;

        /// <summary>
        /// Cached SSL protocol to avoid repeated string comparisons
        /// </summary>
        private readonly SslProtocols _sslProtocol;

        /// <summary>
        /// Buffer size for protocol recognition
        /// </summary>
        private const int ProtocolBufferSize = 8;

        /// <summary>
        /// Creates new connection handler for listening specified port
        /// </summary>
        public ConnectionHandler(HorseServer server, HostListener listener)
        {
            _server = server;
            _listener = listener;
            _sslProtocol = GetProtocol(listener);
        }

        /// <summary>
        /// Accepts new connection requests until stopped
        /// </summary>
        public async Task Handle()
        {
            _listener.KeepAliveManager = new KeepAliveManager();
            _listener.KeepAliveManager.Start(_server.Options.RequestTimeout * 1000);

            while (_server.IsRunning)
            {
                if (_listener.Listener == null)
                    break;

                try
                {
                    //todo: before tcp accept

                    TcpClient tcp = await _listener.Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    tcp.NoDelay = _server.Options.NoDelay;

                    if (_server.Options.QuickAck)
                    {
                        int SIO_TCP_SET_ACK_FREQUENCY = unchecked((int)0x98000017);
                        byte[] outputArray = new byte[128];
                        tcp.Client.IOControl(SIO_TCP_SET_ACK_FREQUENCY, BitConverter.GetBytes(1), outputArray);
                    }

                    _ = AcceptClient(tcp);
                }
                catch (Exception ex)
                {
                    _server.RaiseException(ex);
                }
            }
        }

        /// <summary>
        /// After the client connection request is accepted.
        /// Completes first operations for the client
        /// such as firewall authority, SSL authentication, WebSocket handshaking
        /// </summary>
        private async Task AcceptClient(TcpClient tcp)
        {
            ConnectionInfo info = null;
            byte[] protocolBytes = new byte[ProtocolBufferSize];

            try
            {
                if (_listener == null)
                    return;

                info = new ConnectionInfo(tcp, _listener)
                {
                    State = ConnectionStates.Pending,
                    MaxAlive = DateTime.UtcNow + TimeSpan.FromSeconds(_server.Options.RequestTimeout)
                };

                _listener.KeepAliveManager.Add(info);

                //ssl handshaking
                if (_listener.Options.SslEnabled)
                {
                    SslStream sslStream = _listener.Options.BypassSslValidation
                        ? new SslStream(tcp.GetStream(), true, (a, b, c, d) => true)
                        : new SslStream(tcp.GetStream(), true);

                    info.SslStream = sslStream;
                    SslProtocols protocol = GetProtocol(_listener);
                    info.IsSsl = true;
                    await sslStream.AuthenticateAsServerAsync(_listener.Certificate, false, protocol, false)
                        .ConfigureAwait(false);
                }

                //read one byte and recognize the protocol
                int rc = await info.GetStream()
                    .ReadAsync(protocolBytes, 0, protocolBytes.Length)
                    .ConfigureAwait(false);

                if (rc == 0)
                {
                    info.Close();
                    return;
                }

                //find matched protocol with client protocol
                foreach (IHorseProtocol protocol in _server.Protocols)
                {
                    ProtocolHandshakeResult handshakeResult = await protocol.Handshake(info, protocolBytes)
                        .ConfigureAwait(false);

                    //matched
                    if (handshakeResult.Accepted)
                    {
                        handshakeResult.PreviouslyRead = protocolBytes;
                        protocolBytes = null;
                        info.Protocol = protocol;
                        info.Socket = handshakeResult.Socket;
                        info.Socket?.SetOnConnected();

                        //if protocol required to send protocol message from server to client, send it
                        if (handshakeResult.Response != null)
                            await info.GetStream().WriteAsync(handshakeResult.Response).ConfigureAwait(false);

                        //handle connection events for the connection
                        await protocol.HandleConnection(info, handshakeResult).ConfigureAwait(false);
                        return;
                    }
                }

                info.Close();
            }
            catch (Exception ex)
            {
                info?.Close();
                _server.RaiseException(ex);
            }
        }

        /// <summary>
        /// Disposes connection handler and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_listener.Listener == null)
                return;

            _listener.Listener.Start();

            try
            {
                _listener.Handle.Interrupt();
            }
            catch (Exception ex)
            {
                _server.RaiseException(ex);
            }

            _listener.KeepAliveManager?.Stop();
            _listener.KeepAliveManager = null;
            _listener.Listener = null;
            _listener.Handle = null;
        }

        /// <summary>
        /// Finds supported SSL protocol from server options
        /// </summary>
        private static SslProtocols GetProtocol(HostListener server)
        {
            return server.Options.SslProtocol switch
            {
                "tls" => SslProtocols.Tls,
                "tls11" => SslProtocols.Tls11,
                "tls12" => SslProtocols.Tls12,
                "tls13" => SslProtocols.Tls13,
                _ => SslProtocols.None
            };
        }
    }
}