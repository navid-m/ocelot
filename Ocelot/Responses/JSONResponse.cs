namespace Ocelot.Responses;

public class JSONResponse(string jsonContent) : ResponseType
{
    private readonly string jsonContent = jsonContent;

    public override string ContentType => "application/json";

    public override byte[] GetContent() => System.Text.Encoding.UTF8.GetBytes(jsonContent);
}
