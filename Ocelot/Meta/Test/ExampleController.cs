using Ocelot.Responses;
using Ocelot.Server;

namespace Ocelot.Meta.Test;

public class ExampleController
{
    [Get("/")]
    public static HTMLResponse Index() => new("<h1>Hello</h1>");

    [Get("/json")]
    public static JSONResponse JExample() => new("{\"message\": \"This is a JSON response\"}");

    [Get("/about")]
    public static PlaintextResponse About() => new("This is the about page.");

    [Post("/submit")]
    public static PlaintextResponse Submit(HttpRequest request)
    {
        string formData = request.Body;
        return new PlaintextResponse($"Form data received: {formData}");
    }

    [Get("/increment/{number}")]
    public static PlaintextResponse Increment(string value) =>
        new($"Incremented number: {int.Parse(value) + 1}");

    [Get("/greet/{entity}/{name}")]
    public static PlaintextResponse Greet(string entity, string name) =>
        new($"Hello {name}, the {entity}!");
}
