namespace Ocelot.Server;

public class HttpRequest(
    string? route,
    string? method,
    Dictionary<string, string> headers,
    string body
)
{
    public string? Route { get; } = route;
    public string? Method { get; } = method;
    public Dictionary<string, string> Headers { get; } = headers;
    public string Body { get; } = body;
}
