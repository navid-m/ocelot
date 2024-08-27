using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Ocelot.Responses;
using Ocelot.Server.Exceptions;
using Ocelot.Server.Internal;
using Ocelot.Server.Middleware;

namespace Ocelot.Server;

public class HTTPServer
{
    private readonly Socket _listenerSocket =
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private readonly Dictionary<string, Func<HttpRequest, byte[]>> _routes = [];
    private StaticFileMiddleware? _staticFileMiddleware;
    private readonly string ipAddress;
    private readonly int port;

    public HTTPServer(string ipAddress, int port)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        try
        {
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
            _listenerSocket.ReceiveBufferSize = 32768;
        }
        catch (Exception e)
        {
            throw new AddressInUseException($"The address is already in use: {e}");
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
                _routes[getAttribute.Route] = (request) =>
                {
                    try
                    {
                        return GenerateHttpResponse((Response)method.Invoke(instance, null)!);
                    }
                    catch (InvalidCastException e)
                    {
                        throw new InvalidResponseException($"The response type was not valid: {e}");
                    }
                    catch (Exception e)
                    {
                        throw new ResponseGenerationException(
                            $"Issue generating HTTP response: {e}"
                        );
                    }
                };
            }
            else if (postAttribute != null)
            {
                _routes[postAttribute.Route] = (request) =>
                {
                    try
                    {
                        return GenerateHttpResponse((Response)method.Invoke(instance, [request])!);
                    }
                    catch (InvalidCastException e)
                    {
                        throw new InvalidResponseException($"The response type was not valid: {e}");
                    }
                    catch (Exception e)
                    {
                        throw new ResponseGenerationException(
                            $"Issue generating HTTP response: {e}"
                        );
                    }
                };
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
    private static byte[] GenerateHttpResponse(Response response)
    {
        var content = response.GetContent();
        var headers = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: {response.ContentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n"
        );
        return ContentWriter.CombineHeadersAndResponse(headers, content);
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

            // Use the route with an HttpRequest parameter
            if (_routes.TryGetValue(request.Route, out var handler))
            {
                await networkStream.WriteAsync(handler(request));
            }
            else
            {
                await SendErrorResponse(networkStream, "404 Not Found");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error processing request: {e}");
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

    private static async Task SendErrorResponse(NetworkStream stream, string status)
    {
        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
            )
        );
    }
}
