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

public class HTTPServer
{
    private readonly Socket _listenerSocket;
    private RouteHandler[]? _routes;
    private StaticFileMiddleware? _staticFileMiddleware;
    private readonly string address;
    private readonly int usedPort;
    private readonly byte[] _buffer = new byte[8192];

    public HTTPServer(string ipAddress, int port)
    {
        address = ipAddress;
        usedPort = port;
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
        int index = 0;
        foreach (var method in methods)
        {
            var getAttribute = method.GetCustomAttribute<GetAttribute>();
            var postAttribute = method.GetCustomAttribute<PostAttribute>();
            if (getAttribute != null)
            {
                _routes[index++] = new RouteHandler(getAttribute.Route, method, instance);
            }
            else if (postAttribute != null)
            {
                _routes[index++] = new RouteHandler(postAttribute.Route, method, instance, true);
            }
        }
        if (index < _routes.Length)
        {
            Array.Resize(ref _routes, index);
        }
    }

    public void UseStaticFiles(string rootDirectory) =>
        _staticFileMiddleware = new StaticFileMiddleware(rootDirectory);

    public async ValueTask StartAsync()
    {
        Console.WriteLine($"Go to http://{address}:{usedPort}.");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                _staticFileMiddleware != null
                && _staticFileMiddleware.TryServeFile(request.Route, out var fileResponse)
            )
            {
                await networkStream.WriteAsync(fileResponse);
                return;
            }
            var matchedRoute = Array.Find(_routes!, r => r?.IsMatch(request.Route) ?? false);
            if (matchedRoute != null)
            {
                await networkStream.WriteAsync(matchedRoute.Invoke(request));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public static void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask SendErrorResponse(NetworkStream stream, string status)
    {
        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
            )
        );
    }
}
