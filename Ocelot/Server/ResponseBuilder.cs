using Ocelot.Responses;
using Ocelot.Server.Internal;

namespace Ocelot.Server;

internal static class ResponseBuilder
{
    public static byte[] GenerateHttpResponse(Response response)
    {
        var content = response.GetContent();
        var headers = System.Text.Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: {response.ContentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n"
        );
        return ContentWriter.CombineHeadersAndResponse(headers, content);
    }
}
