using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Ocelot.Server;

public class HTTPServer(string ipAddress, int port)
{
    private readonly Socket _listenerSocket =
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private readonly Dictionary<string, Func<byte[]>> _routes = [];

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
                var response = Encoding.UTF8.GetBytes(method.Invoke(instance, null)!.ToString()!);
                var headers = Encoding.UTF8.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n"
                );
                _routes[attribute.Route] = () => CombineHeadersAndResponse(headers, response);
            }
        }
    }

    public async Task StartAsync()
    {
        _listenerSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
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
            int routeLength = (int)(pRouteEnd - pRouteStart);
            return Encoding.UTF8.GetString(buffer, (int)(pRouteStart - pBuffer), routeLength);
        }
    }

    private static async Task SendErrorResponse(NetworkStream stream, string status)
    {
        var response = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {status.Length}\r\nConnection: close\r\n\r\n{status}"
        );
        await stream.WriteAsync(response);
    }

    private static byte[] CombineHeadersAndResponse(byte[] headers, byte[] response)
    {
        var result = new byte[headers.Length + response.Length];
        Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
        Buffer.BlockCopy(response, 0, result, headers.Length, response.Length);
        return result;
    }
}
