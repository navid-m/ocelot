namespace Ocelot.Responses;

public abstract class Response
{
    public abstract string ContentType { get; }
    public abstract byte[] GetContent();
}
