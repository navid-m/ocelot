using Ocelot.Renderers;
using Ocelot.Responses;
using Ocelot.Server;
using Ocelot.Structures;

namespace Ocelot.Meta.Test;

public class HomeController : IController
{
    [Get("/")]
    public static HTMLResponse Index()
    {
        return ViewRenderer.Render(new { Firstname = "Bill", Lastname = "Gates" }, "index.blade");
    }

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
