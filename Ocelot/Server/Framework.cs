using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Ocelot.Server;

public class OcelotServer(string ipAddress, int port)
{
    private readonly TcpListener _listener = new(IPAddress.Parse(ipAddress), port);
    private readonly Dictionary<string, Func<string>> _routes = [];

    public void RegisterRoutes<T>()
        where T : new()
    {
        var instance = new T();
        var methods = typeof(T).GetMethods();
        foreach (var method in methods)
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
        using (var networkStream = client.GetStream())
        {
            byte[] buffer = new byte[1024];
            int bytesRead = await networkStream.ReadAsync(buffer);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Received request:\n{request}");

            string[] requestLines = request.Split('\n');
            string[] requestParts = requestLines[0].Split(' ');
            if (requestParts.Length >= 2 && requestParts[0] == "GET")
            {
                string route = requestParts[1];
                if (_routes.TryGetValue(route, out var handler))
                {
                    string responseText = handler();
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

        Console.WriteLine("Client disconnected.");
        client.Close();
    }

    private async Task SendResponseAsync(
        NetworkStream stream,
        string status,
        string contentType,
        string content
    )
    {
        string responseHeaders =
            $"HTTP/1.1 {status}\r\n"
            + $"Content-Type: {contentType}\r\n"
            + $"Content-Length: {content.Length}\r\n"
            + "Connection: close\r\n\r\n";

        byte[] responseBytes = Encoding.UTF8.GetBytes(responseHeaders + content);
        await stream.WriteAsync(responseBytes);
    }
}
