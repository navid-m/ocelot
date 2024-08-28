namespace Ocelot.Responses;

public sealed class Text(string textContent) : Response
{
    private readonly string _textContent = textContent;

    public override string ContentType => "text/plain";

    public override byte[] GetContent() => System.Text.Encoding.UTF8.GetBytes(_textContent);
}
