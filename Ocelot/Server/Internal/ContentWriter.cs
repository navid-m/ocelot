namespace Ocelot.Server.Internal;

internal static class ContentWriter
{
    public static byte[] CombineHeadersAndResponse(byte[] headers, byte[] response)
    {
        byte[] result = new byte[headers.Length + response.Length];
        Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
        Buffer.BlockCopy(response, 0, result, headers.Length, response.Length);
        return result;
    }
}
