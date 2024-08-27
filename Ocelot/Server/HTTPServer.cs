using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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
    private readonly Dictionary<string, Func<byte[]>> _routes = [];
    private StaticFileMiddleware? _staticFileMiddleware;
    private readonly string ipAddress;
    private readonly int port;

    public HTTPServer(string ipAddress, int port)
    {
        this.ipAddress = ipAddress;
        this.port = port;

        _listenerSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
    }

    public void RegisterRoutes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T
    >()
        where T : new()
    {
        var instance = new T();
        foreach (var method in typeof(T).GetMethods())
        {
            var attribute = method.GetCustomAttribute<GetAttribute>();
            if (attribute != null)
            {
                _routes[attribute.Route] = () =>
                {
                    try
                    {
                        return GenerateHttpResponse((Response)method.Invoke(instance, null)!);
                    }
                    catch (InvalidCastException e)
                    {
                        throw new InvalidResponseException(
                            $"The response type was not valid: {e.Message}"
                        );
                    }
                    catch (Exception e)
                    {
                        throw new ResponseGenerationException(
                            $"Issue generating HTTP response: {e.Message}"
                        );
                    }
                };
            }
        }
    }

    public void UseStaticFiles(string rootDirectory)
    {
        _staticFileMiddleware = new StaticFileMiddleware(rootDirectory);
    }

    public async Task StartAsync()
    {
        _listenerSocket.Listen(512);

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

    private static byte[] GenerateHttpResponse(Response response)
    {
        var content = response.GetContent();
        var headers = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: {response.ContentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n"
        );
        return ContentWriter.CombineHeadersAndResponse(headers, content);
    }

    private async Task ProcessClientAsync(Socket clientSocket)
    {
        try
        {
            using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
            var buffer = new byte[1024];
            int bytesRead = await networkStream.ReadAsync(buffer);

            if (bytesRead == 0)
            {
                return;
            }

            string? route = ParseRequestRoute(buffer, bytesRead);

            if (route == null)
            {
                await SendErrorResponse(networkStream, "400 Bad Request");
                return;
            }

            // Check static files first
            if (
                _staticFileMiddleware != null
                && _staticFileMiddleware.TryServeFile(route, out var fileResponse)
            )
            {
                await networkStream.WriteAsync(fileResponse);
                return;
            }

            // Fallback to registered routes
            if (_routes.TryGetValue(route, out var handler))
            {
                var responseBytes = handler();
                await networkStream.WriteAsync(responseBytes);
            }
            else
            {
                await SendErrorResponse(networkStream, "404 Not Found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
        }
    }

    private unsafe string? ParseRequestRoute(byte[] buffer, int bytesRead)
    {
        fixed (byte* pBuffer = buffer)
        {
            // Find the end of the request line
            byte* pEnd = pBuffer + bytesRead;
            byte* pLineEnd = pBuffer;
            while (pLineEnd < pEnd && *pLineEnd != (byte)'\n')
            {
                pLineEnd++;
            }

            if (pLineEnd == pBuffer || pLineEnd == pEnd)
            {
                return null;
            }

            // Find the start of the route (first space after the method)
            byte* pRouteStart = pBuffer;
            while (pRouteStart < pLineEnd && *pRouteStart != (byte)' ')
            {
                pRouteStart++;
            }
            pRouteStart++;

            // Find the end of the route (space after the route)
            byte* pRouteEnd = pRouteStart;
            while (pRouteEnd < pLineEnd && *pRouteEnd != (byte)' ')
            {
                pRouteEnd++;
            }

            if (pRouteStart >= pRouteEnd)
            {
                return null;
            }

            // Convert the route to a string
            return Encoding.UTF8.GetString(
                buffer,
                (int)(pRouteStart - pBuffer),
                (int)(pRouteEnd - pRouteStart)
            );
        }
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
