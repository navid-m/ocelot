using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Ocelot.Renderers;
using Ocelot.Reports;
using Ocelot.Server.Exceptions;
using Ocelot.Server.Internal;
using Ocelot.Server.Middleware;

namespace Ocelot.Server;

public sealed class App
{
    private readonly Socket _listenerSocket;
    private RouteHandler[]? _routes;
    private StaticFileMiddleware? _staticFileMiddleware;
    private WebSocketHandler[]? _wsHandlers;
    private readonly string _address;
    private readonly int _usedPort;
    private readonly byte[] _buffer = new byte[8192];

    private readonly Dictionary<string, byte[]> _cache = [];

    public App(string ipAddress, int port)
    {
        _address = ipAddress;
        _usedPort = port;
        _listenerSocket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        )
        {
            ReceiveBufferSize = 8192,
            NoDelay = true
        };
        _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        try
        {
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        }
        catch (Exception e)
        {
            throw new AddressInUseException($"The address is already in use: {e.Message}");
        }
    }

    public void RegisterRoutes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T
    >()
        where T : new()
    {
        T instance = new();
        var methods = typeof(T).GetMethods();
        _routes = new RouteHandler[methods.Length];
        var wsRoutes = new List<WebSocketHandler>();

        int index = 0;
        foreach (var method in methods)
        {
            var getAttribute = method.GetCustomAttribute<GetAttribute>();
            var postAttribute = method.GetCustomAttribute<PostAttribute>();
            var wsAttribute = method.GetCustomAttribute<WsAttribute>();

            if (getAttribute != null)
            {
                _routes[index++] = new RouteHandler(getAttribute.Route, method, instance);
            }
            else if (postAttribute != null)
            {
                _routes[index++] = new RouteHandler(postAttribute.Route, method, instance, true);
            }
            else if (wsAttribute != null)
            {
                wsRoutes.Add(new WebSocketHandler(wsAttribute.Route, method, instance));
            }
        }

        if (index < _routes.Length)
        {
            Array.Resize(ref _routes, index);
        }

        _wsHandlers = [.. wsRoutes];
    }

    public void UseStaticFiles(string rootDirectory) =>
        _staticFileMiddleware = new StaticFileMiddleware(rootDirectory);

    public async ValueTask StartAsync()
    {
        Logger.LogInfo($"Server is at http://{_address}:{_usedPort}.", specifyNoLocation: true);
        _listenerSocket.Listen(2048);
        await AcceptConnectionsAsync();
    }

    private async ValueTask AcceptConnectionsAsync()
    {
        while (true)
        {
            await ProcessClientAsync(
                await Task.Factory.FromAsync(
                    _listenerSocket.BeginAccept,
                    _listenerSocket.EndAccept,
                    null
                )
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ProcessClientAsync(Socket clientSocket)
    {
        try
        {
            using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
            int bytesRead = await networkStream.ReadAsync(_buffer);
            if (bytesRead == 0)
                return;

            var request = ParseHttpRequest(_buffer, bytesRead);
            if (request.Route == null)
            {
                await SendErrorResponse(networkStream, "400 Bad Request");
                return;
            }

            if (
                request.Method == "GET"
                && _cache.TryGetValue(request.Route, out var cachedResponse)
            )
            {
                await networkStream.WriteAsync(cachedResponse);
                return;
            }

            if (
                _staticFileMiddleware != null
                && _staticFileMiddleware.TryServeFile(request.Route, out var fileResponse)
            )
            {
                await networkStream.WriteAsync(fileResponse);
                return;
            }
            if (_wsHandlers != null)
            {
                WebSocketHandler? matchedWsRoute;
                try
                {
                    matchedWsRoute = _wsHandlers.FirstOrDefault(h => h.IsMatch(request.Route));
                }
                catch (Exception e)
                {
                    Logger.LogIssue($"Error processing websocket route: {e.Message}");
                    throw;
                }
                if (matchedWsRoute != null)
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://{_address}:{_usedPort}/");
                    listener.Start();

                    HttpListenerContext context = await listener.GetContextAsync();

                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                        await matchedWsRoute.HandleWebSocketAsync(wsContext.WebSocket);
                    }

                    listener.Stop();
                    return;
                }
            }
            var matchedRoute = Array.Find(_routes!, r => r?.IsMatch(request.Route) ?? false);
            if (matchedRoute != null)
            {
                var responseBytes = matchedRoute.Invoke(request);
                if (request.Method == "GET" && responseBytes.Length <= 8192)
                {
                    _cache[request.Route] = responseBytes;
                }
                await networkStream.WriteAsync(responseBytes);
            }
            else
            {
                await SendErrorResponse(networkStream, "404 Not Found");
            }
        }
        catch (Exception e)
        {
            Logger.LogIssue($"Error processing request: {e.Message}\nTrace: {e.StackTrace}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static HttpRequest ParseHttpRequest(byte[] buffer, int bytesRead)
    {
        string requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        string[] lines = requestText.Split("\r\n");
        string[] requestLine = lines[0].Split(' ');

        if (requestLine.Length < 3)
        {
            return new HttpRequest(null, null, [], string.Empty);
        }

        Dictionary<string, string> headers = [];
        string method = requestLine[0];
        int i = 1;

        while (!string.IsNullOrWhiteSpace(lines[i]))
        {
            string[] headerParts = lines[i].Split(':', 2);
            if (headerParts.Length == 2)
            {
                headers[headerParts[0].Trim()] = headerParts[1].Trim();
            }
            i++;
        }

        string body = string.Empty;
        if (method == "POST" && headers.TryGetValue("Content-Length", out string? contentLength))
        {
            body = requestText.Substring(
                requestText.IndexOf("\r\n\r\n") + 4,
                int.Parse(contentLength)
            );
        }

        return new HttpRequest(requestLine[1], method, headers, body);
    }

    public void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);

    private static async ValueTask SendErrorResponse(NetworkStream stream, string status)
    {
        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
            )
        );
    }
}
