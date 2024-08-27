using Ocelot.Responses;

namespace Ocelot.Server.Internal;

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
