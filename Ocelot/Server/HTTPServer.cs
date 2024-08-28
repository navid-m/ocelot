using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs.Http;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Ocelot.Renderers;
using Ocelot.Reports;
using Ocelot.Server.Exceptions;
using Ocelot.Server.Internal;
using Ocelot.Server.Middleware;

namespace Ocelot.Server;

public sealed class App(string ipAddress, int port)
{
    private RouteHandler[]? _routes;
    private StaticFileMiddleware? _staticFileMiddleware;
    private WebSocketHandler[]? _wsHandlers;
    private readonly string _address = ipAddress;
    private readonly int _port = port;
    private readonly ConcurrentDictionary<string, byte[]> _cache = [];
    private MultithreadEventLoopGroup? _bossGroup;
    private MultithreadEventLoopGroup? _workerGroup;
    private IChannel? _boundChannel;

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
        _bossGroup = new MultithreadEventLoopGroup(1);
        _workerGroup = new MultithreadEventLoopGroup();
        try
        {
            var bootstrap = new ServerBootstrap();
            bootstrap
                .Group(_bossGroup, _workerGroup)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 8192)
                .ChildOption(ChannelOption.TcpNodelay, true)
                .ChildHandler(
                    new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        channel.Pipeline.AddLast(new HttpServerCodec());
                        channel.Pipeline.AddLast(new HttpObjectAggregator(65536));
                        channel.Pipeline.AddLast(new AppServerHandler(this));
                    })
                );
            _boundChannel = await bootstrap.BindAsync(IPAddress.Parse(_address), _port);
            Logger.LogInfo($"Server is at http://{_address}:{_port}.", specifyNoLocation: true);
            await _boundChannel.CloseCompletion;
        }
        catch (Exception ex)
        {
            Logger.LogIssue($"An error occurred: {ex.Message}");
            throw new AddressInUseException($"The address is already in use: {ex.Message}");
        }
        finally
        {
            await Task.WhenAll(
                _bossGroup.ShutdownGracefullyAsync(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(1)
                ),
                _workerGroup.ShutdownGracefullyAsync(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(1)
                )
            );
        }
    }

    public static void UseTemplatePath(string path) => ViewRenderer.SetTemplatesPath(path);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal async Task HandleRequestAsync(IChannelHandlerContext ctx, IFullHttpRequest request)
    {
        var uri = request.Uri;
        var method = request.Method.ToString();

        if (
            method == DotNetty.Codecs.Http.HttpMethod.Get.ToString()
            && _cache.TryGetValue(uri, out var cachedResponse)
        )
        {
            await SendResponseAsync(ctx, Unpooled.WrappedBuffer(cachedResponse));
            return;
        }

        if (
            _staticFileMiddleware != null
            && _staticFileMiddleware.TryServeFile(uri, out var fileResponse)
        )
        {
            await SendResponseAsync(ctx, Unpooled.WrappedBuffer(fileResponse));
            return;
        }

        if (_wsHandlers != null)
        {
            var matchedWsRoute = _wsHandlers.FirstOrDefault(h => h.IsMatch(uri));
            if (matchedWsRoute != null)
            {
                // WebSocket handling logic here
                // This part requires additional implementation to integrate with DotNetty's WebSocket support
                return;
            }
        }

        var matchedRoute = Array.Find(_routes!, r => r?.IsMatch(uri) ?? false);
        if (matchedRoute != null)
        {
            var httpRequest = new HttpRequest(
                uri,
                method,
                GetHeaders(request.Headers),
                GetBody(request)
            );
            var responseBytes = matchedRoute.Invoke(httpRequest);

            if (
                method == DotNetty.Codecs.Http.HttpMethod.Get.ToString()
                && responseBytes.Length <= 32768
            )
            {
                _cache[uri] = responseBytes;
            }

            await SendResponseAsync(ctx, Unpooled.WrappedBuffer(responseBytes));
        }
        else
        {
            await SendErrorResponseAsync(ctx, HttpResponseStatus.NotFound);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, string> GetHeaders(HttpHeaders headers)
    {
        var result = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            result[header.Key.ToString()] = header.Value.ToString();
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetBody(IFullHttpRequest request)
    {
        var content = request.Content;
        if (content.IsReadable())
        {
            var buffer = new byte[content.ReadableBytes];
            content.ReadBytes(buffer);
            return Encoding.UTF8.GetString(buffer);
        }
        return string.Empty;
    }

    private static async Task SendResponseAsync(IChannelHandlerContext ctx, IByteBuffer content)
    {
        var response = new DefaultFullHttpResponse(
            DotNetty.Codecs.Http.HttpVersion.Http11,
            HttpResponseStatus.OK,
            content
        );
        response.Headers.Set(HttpHeaderNames.ContentType, "text/plain");
        response.Headers.Set(HttpHeaderNames.ContentLength, content.ReadableBytes);
        await ctx.WriteAndFlushAsync(response);
    }

    private static async Task SendErrorResponseAsync(
        IChannelHandlerContext ctx,
        HttpResponseStatus status
    )
    {
        var response = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, status);
        response.Headers.Set(HttpHeaderNames.ContentType, "text/plain");
        response.Headers.Set(HttpHeaderNames.ContentLength, 0);
        await ctx.WriteAndFlushAsync(response);
    }
}
