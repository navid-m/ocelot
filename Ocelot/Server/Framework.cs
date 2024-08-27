using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Ocelot.Server;

public class HTTPServer(string ipAddress, int port)
{
    private readonly TcpListener _listener = new(IPAddress.Parse(ipAddress), port);
    private readonly Dictionary<string, Func<string>> _routes = [];

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
                _routes[attribute.Route] = () => method.Invoke(instance, null)!.ToString()!;
            }
        }
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine(
            $@"
Server started on: 
http://{((IPEndPoint)_listener.LocalEndpoint).Address}:{((IPEndPoint)_listener.LocalEndpoint).Port}
"
        );

        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = ProcessClientAsync(client);
        }
    }

    private async Task ProcessClientAsync(TcpClient client)
    {
        try
        {
            using var networkStream = client.GetStream();
            using var streamReader = new StreamReader(
                networkStream,
                Encoding.UTF8,
                leaveOpen: true
            );
            var requestLine = await streamReader.ReadLineAsync();
            if (requestLine == null)
            {
                await SendResponseAsync(
                    networkStream,
                    "400 Bad Request",
                    "text/plain",
                    "Bad Request"
                );
                return;
            }

            string[]? requestParts = requestLine.Split(' ');

            if (requestParts.Length >= 2 && requestParts[0] == "GET")
            {
                var route = requestParts[1];
                if (_routes.TryGetValue(route, out var handler))
                {
                    var responseText = handler();
                    await SendResponseAsync(networkStream, "200 OK", "text/plain", responseText);
                }
                else
                {
                    await SendResponseAsync(
                        networkStream,
                        "404 Not Found",
                        "text/plain",
                        "Not Found"
                    );
                }
            }
            else
            {
                await SendResponseAsync(
                    networkStream,
                    "400 Bad Request",
                    "text/plain",
                    "Bad Request"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private static async Task SendResponseAsync(
        NetworkStream stream,
        string status,
        string contentType,
        string content
    )
    {
        string headers =
            $"HTTP/1.1 {status}\r\n"
            + $"Content-Type: {contentType}\r\n"
            + $"Content-Length: {content.Length}\r\n"
            + "Connection: close\r\n\r\n";

        byte[]? headersBytes = Encoding.UTF8.GetBytes(headers);
        byte[]? contentBytes = Encoding.UTF8.GetBytes(content);

        using var memoryStream = new MemoryStream(headersBytes.Length + contentBytes.Length);
        await memoryStream.WriteAsync(headersBytes);
        await memoryStream.WriteAsync(contentBytes);

        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(stream);
    }
}
