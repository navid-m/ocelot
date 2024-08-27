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
    private readonly Socket _listenerSocket =
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private readonly List<RouteHandler> _routes = [];
    private StaticFileMiddleware? _staticFileMiddleware;
    private readonly string ipAddress;
    private readonly int port;

    public HTTPServer(string ipAddress, int port)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        try
        {
            _listenerSocket.ReceiveBufferSize = 32768;
            _listenerSocket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.KeepAlive,
                true
            );
            _listenerSocket.NoDelay = true;
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
        foreach (var method in typeof(T).GetMethods())
        {
            GetAttribute? getAttribute = method.GetCustomAttribute<GetAttribute>();
            PostAttribute? postAttribute = method.GetCustomAttribute<PostAttribute>();

            if (getAttribute != null)
            {
                _routes.Add(new RouteHandler(getAttribute.Route, method, instance));
            }
            else if (postAttribute != null)
            {
                _routes.Add(new RouteHandler(postAttribute.Route, method, instance, true));
            }
        }
    }

    public void UseStaticFiles(string rootDirectory) =>
        _staticFileMiddleware = new StaticFileMiddleware(rootDirectory);

    public async Task StartAsync()
    {
        _listenerSocket.Listen(1024);

        Console.WriteLine($"Server started on: http://{ipAddress}:{port}\n");

        while (true)
        {
            var clientSocket = await Task.Factory.FromAsync(
                _listenerSocket.BeginAccept,
                _listenerSocket.EndAccept,
                null
            );
            _ = Task.Run(() => ProcessClientAsync(clientSocket));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task ProcessClientAsync(Socket clientSocket)
    {
        try
        {
            using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
            byte[] buffer = new byte[32768];
            int bytesRead = await networkStream.ReadAsync(buffer);

            if (bytesRead == 0)
            {
                return;
            }

            HttpRequest request = ParseHttpRequest(buffer, bytesRead);
            if (request.Route == null)
            {
                await SendErrorResponse(networkStream, "400 Bad Request");
                return;
            }

            // Check static files first
            if (
                _staticFileMiddleware != null
                && _staticFileMiddleware.TryServeFile(request.Route, out var fileResponse)
            )
            {
                await networkStream.WriteAsync(fileResponse);
                return;
            }

            // Use the route handler with pattern matching
            var matchedRoute = _routes.FirstOrDefault(r => r.IsMatch(request.Route));
            if (matchedRoute != null)
            {
                var responseBytes = matchedRoute.Invoke(request);
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

        string method = requestLine[0];
        string route = requestLine[1];

        Dictionary<string, string> headers = [];
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
        return new HttpRequest(route, method, headers, body);
    }

    public static void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);

    private static async Task SendErrorResponse(NetworkStream stream, string status)
    {
        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
            )
        );
    }
}
