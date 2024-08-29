using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Ocelot.Renderers;
using Ocelot.Reports;
using Ocelot.Server.Internal;
using Ocelot.Server.Middleware;

namespace Ocelot.Server;

public sealed class App(string ipAddress, int port)
{
    private RouteHandler[]? _routes;
    private StaticFileMiddleware? _staticFileMiddleware;
    private WebSocketHandler[]? _wsHandlers;
    private readonly string _address = ipAddress;
    private readonly int _usedPort = port;
    private static readonly int _minBufSize = 32768;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask StartAsync()
    {
        Logger.LogInfo($"Server is at http://{_address}:{_usedPort}.", specifyNoLocation: true);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://{_address}:{_usedPort}/");
        listener.Start();

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                await ProcessClientAsync(context);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ProcessClientAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // Read request body if necessary
            string body = string.Empty;
            if (request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                body = await reader.ReadToEndAsync();
            }

            // Parse the request URL and determine route
            string route = request.Url?.AbsolutePath ?? string.Empty;

            if (route == string.Empty || request.HttpMethod == string.Empty)
            {
                await SendErrorResponse(response, "400 Bad Request");
                return;
            }

            if (request.HttpMethod == "GET" && _cache.TryGetValue(route, out byte[]? responseBytes))
            {
                await WriteResponseAsync(response, responseBytes);
                return;
            }

            if (
                _staticFileMiddleware != null
                && _staticFileMiddleware.TryServeFile(route, response)
            )
            {
                return;
            }

            if (_wsHandlers != null)
            {
                WebSocketHandler? matchedWsRoute = _wsHandlers.FirstOrDefault(h =>
                    h.IsMatch(route)
                );
                if (matchedWsRoute != null)
                {
                    if (context.Request.IsWebSocketRequest)
                    {
                        await matchedWsRoute.HandleWebSocketAsync(
                            (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket
                        );
                        return;
                    }
                }
            }

            var matchedRoute = Array.Find(_routes!, r => r?.IsMatch(route) ?? false);

            if (matchedRoute != null)
            {
                responseBytes = matchedRoute.Invoke(
                    new HttpRequest(
                        route,
                        request.HttpMethod,
                        request
                            .Headers.AllKeys.Where(k => k != null)
                            .ToDictionary(k => k!, k => request.Headers[k] ?? string.Empty),
                        body
                    ),
                    response
                );

                if (request.HttpMethod == "GET" && responseBytes.Length <= _minBufSize)
                {
                    _cache[route] = responseBytes;
                }
                await WriteResponseAsync(response, responseBytes);
            }
            else
            {
                await SendErrorResponse(response, "404 Not Found");
            }
        }
        catch (Exception e)
        {
            Logger.LogIssue($"Error processing request: {e.Message}\nTrace: {e.StackTrace}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask WriteResponseAsync(
        HttpListenerResponse response,
        byte[] responseBytes
    )
    {
        using var outputStream = response.OutputStream;
        await outputStream.WriteAsync(responseBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask SendErrorResponse(HttpListenerResponse response, string status)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
        );
        response.StatusCode = int.Parse(status.Split(' ')[0]);
        response.ContentLength64 = responseBytes.Length;
        using var outputStream = response.OutputStream;
        await outputStream.WriteAsync(responseBytes);
    }

    public void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);
}
