namespace Ocelot.Responses;

public abstract class ResponseType
{
    public abstract string ContentType { get; }
    public abstract byte[] GetContent();
}
