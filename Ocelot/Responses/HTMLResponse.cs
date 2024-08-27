using System.Text;

namespace Ocelot.Responses;

public class HTMLResponse(string htmlContent) : ResponseType
{
    private readonly string _htmlContent = htmlContent;

    public override string ContentType => "text/html";

    public override byte[] GetContent() => Encoding.UTF8.GetBytes(_htmlContent);
}
