using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Ocelot.Renderers;
using Ocelot.Reports;
using Ocelot.Server.Internal;
using Ocelot.Server.Middleware;

namespace Ocelot.Server;

public sealed class App
{
    private RouteHandler[]? _routes;
    private StaticFileMiddleware? _staticFileMiddleware;
    private WebSocketHandler[]? _wsHandlers;
    private readonly string _address;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly Channel<HttpListenerContext> _requestQueue;
    private static readonly int _minBufSize = 32768;

    public App(string ipAddress, int port)
    {
        _address = ipAddress;
        _port = port;
        _requestQueue = Channel.CreateBounded<HttpListenerContext>(
            new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            }
        );
        ThreadPool.SetMinThreads(100, 100);
        ThreadPool.SetMaxThreads(
            Environment.ProcessorCount * 100,
            Environment.ProcessorCount * 100
        );
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
        Logger.LogInfo($"Server is running at http://{_address}:{_port}", specifyNoLocation: true);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://{_address}:{_port}/");
        listener.Start();

        var processingTasks = Enumerable
            .Range(0, Environment.ProcessorCount)
            .Select(_ => ProcessRequestsAsync())
            .ToArray();

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                await _requestQueue.Writer.WriteAsync(context);
            }
        }
        finally
        {
            listener.Stop();
            _requestQueue.Writer.Complete();
            await Task.WhenAll(processingTasks);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task ProcessRequestsAsync()
    {
        await foreach (var context in _requestQueue.Reader.ReadAllAsync())
        {
            _ = ProcessClientAsync(context);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task ProcessClientAsync(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string body = string.Empty;
            if (request.HttpMethod == "POST")
            {
                body = await new StreamReader(request.InputStream, Encoding.UTF8).ReadToEndAsync();
            }

            string route = request.Url?.AbsolutePath ?? string.Empty;

            if (string.IsNullOrEmpty(route) || string.IsNullOrEmpty(request.HttpMethod))
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
                if (matchedWsRoute != null && context.Request.IsWebSocketRequest)
                {
                    await matchedWsRoute.HandleWebSocketAsync(
                        (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket
                    );
                    return;
                }
            }

            RouteHandler? matchedRoute = Array.Find(_routes!, r => r?.IsMatch(route) ?? false);

            if (matchedRoute != null)
            {
                responseBytes = matchedRoute.Invoke(
                    new HttpRequest(
                        route,
                        request.HttpMethod,
                        request.Headers.AllKeys.ToDictionary(
                            k => k!,
                            k => request.Headers[k] ?? string.Empty
                        ),
                        body
                    ),
                    response
                );

                if (request.HttpMethod == "GET" && responseBytes.Length <= _minBufSize)
                {
                    if (_cache.Count >= 262144)
                    {
                        string? oldestKey = _cache.Keys.OrderBy(k => k).FirstOrDefault();
                        if (oldestKey != null)
                        {
                            _cache.TryRemove(oldestKey, out _);
                        }
                    }
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
        response.ContentLength64 = responseBytes.Length;
        response.ContentEncoding = Encoding.UTF8;
        await response.OutputStream.WriteAsync(responseBytes).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask SendErrorResponse(HttpListenerResponse response, string status)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
        );
        response.StatusCode = int.Parse(status.Split(' ')[0]);
        response.ContentLength64 = responseBytes.Length;
        await response.OutputStream.WriteAsync(responseBytes).ConfigureAwait(false);
    }

    public void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);
}
