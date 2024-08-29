using System.Net;

namespace Ocelot.Server.Middleware;

public class StaticFileMiddleware
{
    private readonly string _rootDirectory;

    public StaticFileMiddleware(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public bool TryServeFile(string route, HttpListenerResponse response)
    {
        // Convert route to file path
        string filePath = Path.Combine(
            _rootDirectory,
            route.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(filePath))
            return false;

        byte[] fileBytes = File.ReadAllBytes(filePath);
        response.ContentType = GetContentType(Path.GetExtension(filePath));
        response.ContentLength64 = fileBytes.Length;
        response.StatusCode = (int)HttpStatusCode.OK;

        using var outputStream = response.OutputStream;
        outputStream.Write(fileBytes, 0, fileBytes.Length);
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
}
