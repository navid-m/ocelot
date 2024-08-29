using System.Net;
using System.Runtime.CompilerServices;

namespace Ocelot.Web.Middleware;

public sealed class StaticFileMiddleware(string rootDirectory)
{
    private readonly string _rootDirectory = rootDirectory;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryServeFile(string route, HttpListenerResponse response)
    {
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
        response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
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
