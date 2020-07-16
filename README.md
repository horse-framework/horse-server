
# Twino Server

Twino Server is a TCP Server and includes Core modules for Twino Framework Libraries.
Only use Twino Server directly if you develop new protocol extension for twino server.

If you are looking for Messaging Queue Server, HTTP Server with MVC Architecture, WebSocket Server/Client or IOC Library, you can check other repositories of Twino Framework.

## NuGet Packages

**[Twino Server](https://www.nuget.org/packages/Twino.Server)**<br>
**[Twino Core Module](https://www.nuget.org/packages/Twino.Core)**<br>
**[Client Connector Library](https://www.nuget.org/packages/Twino.Client.Connectors)**<br>


### Usage

TwinoServer is an object, accepts TCP requests and finds the protocol which accepts the request. Here is a quick example which is used for accepting websocket connections used in twino websocket library.

    public class ServerWsHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
    {
      ...
    }
    
    ...
    
    ServerWsHandler handler = new ServerWsHandler();
    TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
    server.UseWebSockets(handler);
    server.Start();
    

UseWebSockets is an extension method. If you need to add another protocol, you can create your own extension method that calls TwinoServer's UseProtocol method. Or you can just add protocol to the server like this:

    public class CustomProtocol : ITwinoProtocol
    {
      ...
    }
    
    ...
    
    server.UseProtocol(new CustomProtocol());
    
