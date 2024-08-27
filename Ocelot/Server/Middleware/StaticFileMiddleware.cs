using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ocelot.Server.Middleware;

public class StaticFileMiddleware(string rootDirectory)
{
    private readonly string _rootDirectory = rootDirectory;

    public bool TryServeFile(string route, [NotNullWhen(true)] out byte[]? response)
    {
        response = null;

        // Convert route to file path
        var filePath = Path.Combine(
            _rootDirectory,
            route.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(filePath))
        {
            return false;
        }

        var fileBytes = File.ReadAllBytes(filePath);
        var contentType = GetContentType(Path.GetExtension(filePath));
        var headers = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {fileBytes.Length}\r\nConnection: close\r\n\r\n"
        );
        response = CombineHeadersAndResponse(headers, fileBytes);
        return true;
    }

    private static string GetContentType(string extension) =>
        extension.ToLower() switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };

    private static byte[] CombineHeadersAndResponse(byte[] headers, byte[] response)
    {
        var result = new byte[headers.Length + response.Length];
        Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
        Buffer.BlockCopy(response, 0, result, headers.Length, response.Length);
        return result;
    }
}
