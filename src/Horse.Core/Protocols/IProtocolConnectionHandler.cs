using System.Threading.Tasks;

namespace Horse.Core.Protocols
{
    /// <summary>
    /// Horse Protocol Connection handler implementation
    /// </summary>
    public interface IProtocolConnectionHandler<TClient, in TMessage>
        where TClient : SocketBase
    {
        /// <summary>
        /// Triggered when a piped client is connected. 
        /// </summary>
        Task<TClient> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data);

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        Task Ready(IHorseServer server, TClient client);

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        Task Received(IHorseServer server, IConnectionInfo info, TClient client, TMessage message);

        /// <summary>
        /// Triggered when a piped client is disconnected. 
        /// </summary>
        Task Disconnected(IHorseServer server, TClient client);
    }
}