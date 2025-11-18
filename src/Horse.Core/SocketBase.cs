using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core.Protocols;

[assembly: InternalsVisibleTo("Horse.Server")]

namespace Horse.Core
{
    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void SocketStatusHandler(SocketBase client);

    /// <summary>
    /// Base class for web socket clients.
    /// Server-side client and Client-side client classes are derived from this class
    /// </summary>
    public abstract class SocketBase
    {
        #region Properties

        /// <summary>
        /// TcpClient class of the socket class
        /// </summary>
        protected TcpClient Client { get; set; }

        /// <summary>
        /// TcpClient network stream (with SSL or without SSL, depends on the requested URL or server certificate)
        /// </summary>
        protected Stream Stream { get; set; }

        /// <summary>
        /// Client's connected status
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Gets the connection is over SSL or not
        /// </summary>
        public bool IsSsl { get; protected set; }

        /// <summary>
        /// When client is disconnected and disposed,
        /// The message will be sent to all event subscribers.
        /// Sometimes multiple errors occur when the connection is failed.
        /// To avoid multiple event fires, this field is used.
        /// </summary>
        private volatile bool _disconnectedWarn;

        /// <summary>
        /// SslStream does not support concurrent write operations.
        /// This semaphore is used to handle that issue
        /// </summary>
        private readonly SemaphoreSlim _ss = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Triggered when the client is connected
        /// </summary>
        public event SocketStatusHandler Connected;

        /// <summary>
        /// Triggered when the client is disconnected
        /// </summary>
        public event SocketStatusHandler Disconnected;

        /// <summary>
        /// Last message received or sent date.
        /// Used for preventing unnecessary ping/pong traffic
        /// </summary>
        public DateTime LastAliveDate { get; protected set; } = DateTime.UtcNow;

        /// <summary>
        /// True, If a pong must received asap
        /// </summary>
        internal bool PongRequired { get; set; }

        /// <summary>
        /// When true, If socket sends or receives messages in last ping interval duration, sending PING messages is discarded.
        /// When false, PING messages are sent always.
        /// Default value is true.
        /// </summary>
        public bool SmartHealthCheck { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Socket base constructor for client sockets
        /// </summary>
        protected SocketBase()
        {
        }

        /// <summary>
        /// Socket base constructor for server sockets
        /// </summary>
        protected SocketBase(IConnectionInfo info)
        {
            IsSsl = info.IsSsl;
            IsConnected = true;
            Stream = info.GetStream();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ends write operation and completed callback
        /// </summary>
        private void EndWrite(IAsyncResult ar)
        {
            try
            {
                Stream.EndWrite(ar);
                ReleaseSslLock();
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public async Task<bool> SendAsync(byte[] data)
        {
            try
            {
                if (Stream == null || data == null)
                {
                    ReleaseSslLock();
                    return false;
                }

                if (IsSsl)
                    await _ss.WaitAsync().ConfigureAwait(false);

                await Stream.WriteAsync(data).ConfigureAwait(false);

                if (IsSsl)
                    ReleaseSslLock();

                return true;
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public async Task<bool> SendAsync(ReadOnlyMemory<byte> data)
        {
            try
            {
                if (Stream == null)
                {
                    ReleaseSslLock();
                    return false;
                }

                if (IsSsl)
                    await _ss.WaitAsync().ConfigureAwait(false);

                await Stream.WriteAsync(data).ConfigureAwait(false);

                if (IsSsl)
                    ReleaseSslLock();

                return true;
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public bool Send(ReadOnlySpan<byte> data)
        {
            try
            {
                if (Stream == null)
                    return false;

                if (IsSsl)
                    _ss.Wait();

                Stream.Write(data);
                return true;
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public bool Send(byte[] data)
        {
            try
            {
                if (Stream == null || data == null)
                    return false;

                if (IsSsl)
                    _ss.Wait();

                Stream.BeginWrite(data, 0, data.Length, EndWrite, data);
                return true;
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public void Send(byte[] data, Action<bool> sendCallback)
        {
            try
            {
                if (Stream == null || data == null)
                {
                    sendCallback(false);
                    return;
                }

                if (IsSsl)
                    _ss.Wait();

                Stream.BeginWrite(data, 0, data.Length, ar =>
                {
                    try
                    {
                        Stream.EndWrite(ar);

                        if (IsSsl)
                            ReleaseSslLock();

                        sendCallback(true);
                    }
                    catch
                    {
                        sendCallback(false);
                        ReleaseSslLock();
                        Disconnect();
                    }
                }, data);
            }
            catch
            {
                sendCallback(false);
                Disconnect();
            }
        }

        /// <summary>
        /// Updates socket alive date
        /// </summary>
        public void KeepAlive()
        {
            LastAliveDate = DateTime.UtcNow;
            PongRequired = false;
        }

        /// <summary>
        /// Releases ssl semaphore 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseSslLock()
        {
            if (IsSsl && _ss?.CurrentCount == 0)
            {
                try
                {
                    _ss.Release();
                }
                catch
                {
                }
            }
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Disconnects client and disposes all streams belongs it
        /// </summary>
        public virtual void Disconnect()
        {
            try
            {
                IsConnected = false;

                if (Stream != null)
                    Stream.Dispose();

                if (Client != null)
                    Client.Close();

                Client = null;
                Stream = null;
            }
            catch
            {
            }

            if (!_disconnectedWarn)
            {
                _disconnectedWarn = true;
                OnDisconnected();
            }
        }

        /// <summary>
        /// Triggered when client is connected
        /// </summary>
        protected virtual void OnConnected()
        {
            Connected?.Invoke(this);
        }

        /// <summary>
        /// Triggered when client is disconnected
        /// </summary>
        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        /// <summary>
        /// Called when client's protocol has switched
        /// </summary>
        protected virtual void OnProtocolSwitched(IHorseProtocol previous, IHorseProtocol current)
        {
            //not abstract, override is not must. but we do not have anything to do here.
        }

        /// <summary>
        /// Triggers virtual connected method
        /// </summary>
        internal void SetOnConnected()
        {
            OnConnected();
        }

        /// <summary>
        /// Triggers virtual protocol switched
        /// </summary>
        internal void SetOnProtocolSwitched(IHorseProtocol previous, IHorseProtocol current)
        {
            OnProtocolSwitched(previous, current);
        }

        /// <summary>
        /// Sends ping
        /// </summary>
        /// <returns></returns>
        public abstract void Ping();

        /// <summary>
        /// Sends pong
        /// </summary>
        /// <returns></returns>
        public abstract void Pong(object pingMessage = null);

        #endregion
    }
}