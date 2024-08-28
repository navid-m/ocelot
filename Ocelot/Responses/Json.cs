namespace Ocelot.Responses;

public sealed class Json(string jsonContent) : Response
{
    private readonly string jsonContent = jsonContent;

    public override string ContentType => "application/json";

    public override byte[] GetContent() => System.Text.Encoding.UTF8.GetBytes(jsonContent);
}
