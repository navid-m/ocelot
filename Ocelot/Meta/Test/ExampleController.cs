using Ocelot.Responses;
using Ocelot.Server;

namespace Ocelot.Meta.Test;

public class ExampleController
{
    [Get("/")]
    public static HTMLResponse Index()
    {
        return new HTMLResponse("<h1>Hello</h1>");
    }

    [Get("/json")]
    public static JSONResponse JExample()
    {
        return new JSONResponse("{\"message\": \"This is a JSON response\"}");
    }

    [Get("/about")]
    public static PlaintextResponse About()
    {
        return new PlaintextResponse("This is the about page.");
    }
}
