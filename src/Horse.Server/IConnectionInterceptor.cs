using System.Net.Sockets;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Server;

public interface IConnectionInterceptor
{
    Task OnBeforeAcceptTcpClient(TcpListener listener);

    Task OnAfterProtocolHandshake(SocketBase socket, IHorseProtocol protocol);
}