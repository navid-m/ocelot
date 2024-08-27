using Ocelot.Responses;
using Ocelot.Server;

namespace Ocelot.Test;

public class ExampleController
{
    [Get("/")]
    public static JSONResponse Index()
    {
        return new JSONResponse("{\"message\": \"This is a JSON response\"}");
    }

    [Get("/about")]
    public static PlaintextResponse About()
    {
        return new PlaintextResponse("This is the about page.");
    }
}
