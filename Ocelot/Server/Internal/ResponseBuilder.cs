using Ocelot.Responses;

namespace Ocelot.Server.Internal;

internal static class ResponseBuilder
{
    public static byte[] GenerateHttpResponse(Response response)
    {
        byte[] content = response.GetContent();
        return ContentWriter.CombineHeadersAndResponse(
            System.Text.Encoding.UTF8.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: {response.ContentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n"
            ),
            content
        );
    }
}
