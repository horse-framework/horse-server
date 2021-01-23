using System.Threading.Tasks;
using Horse.Core.Protocols;

namespace Horse.Core
{
    /// <summary>
    /// Horse TCP Server implementation
    /// </summary>
    public interface IHorseServer
    {
        /// <summary>
        /// Logger class for Server operations.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Pinger for piped clients that connect and stay alive for a long time
        /// </summary>
        IHeartbeatManager HeartbeatManager { get; }

        /// <summary>
        /// Uses the protocol for new TCP connections that request the protocol
        /// </summary>
        void UseProtocol(IHorseProtocol protocol);

        /// <summary>
        /// Switches client's protocol to new protocol (finds by name)
        /// </summary>
        Task SwitchProtocol(IConnectionInfo info, string newProtocolName, ConnectionData data);

        /// <summary>
        /// Finds protocol by name
        /// </summary>
        IHorseProtocol FindProtocol(string name);
    }
}