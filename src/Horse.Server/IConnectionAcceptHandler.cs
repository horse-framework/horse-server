using System.Net.Sockets;
using System.Threading.Tasks;

namespace Horse.Server;

/// <summary>
/// Connection accept handler intercepts new connection requests
/// </summary>
public interface IConnectionAcceptHandler
{
    /// <summary>
    /// Executed before accepting a new connection.
    /// If you need to slower accepting connections you can wait here.
    /// </summary>
    public Task BeforeAccept(HorseServer server, ConnectionHandler handler);

    /// <summary>
    /// Executed after accepting a new connection.
    /// If returns false, connection will be rejected.
    /// </summary>
    public Task<bool> AfterAccept(HorseServer server, ConnectionHandler handler, TcpClient client);
}