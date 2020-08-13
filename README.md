
# Twino Server

[![NuGet](https://img.shields.io/nuget/v/Twino.Server)](https://www.nuget.org/packages/Twino.Server)

Twino Server is a TCP Server and includes Core modules for Twino Framework Libraries.
Only use Twino Server directly if you develop new protocol extension for twino server.

If you are looking for Messaging Queue Server, HTTP Server with MVC Architecture, WebSocket Server/Client or IOC Library, you can check other repositories of Twino Framework.


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
    


## Thanks

Thanks to JetBrains for a open source license to use on this project.

[![jetbrains](https://user-images.githubusercontent.com/21208762/90192662-10043700-ddcc-11ea-9533-c43b99801d56.png)](https://www.jetbrains.com/?from=twino-framework)
