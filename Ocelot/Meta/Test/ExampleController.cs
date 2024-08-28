using Ocelot.Renderers.Models;
using Ocelot.Responses;
using Ocelot.Server;
using Ocelot.Structures;

namespace Ocelot.Meta.Test;

public class HomeController : IController
{
    [Get("/")]
    public static View Index()
    {
        return Render(
            new ViewModel
            {
                { "Firstname", "Bill" },
                { "Lastname", "Gates" },
                { "Something", "Something else" }
            },
            "index"
        );
    }

    [Post("/submit")]
    public static Text Submit(HttpRequest request)
    {
        return new($"Form data received: {request.Body}");
    }

    [Get("/json")]
    public static Json JExample() => new("{\"message\": \"This is a JSON response\"}");

    [Get("/about")]
    public static Text About() => new("This is the about page.");

    [Get("/increment/{number}")]
    public static Text Increment(string value) =>
        new($"Incremented number: {int.Parse(value) + 1}");

    [Get("/greet/{entity}/{name}")]
    public static Text Greet(string entity, string name) => new($"Hello {name}, the {entity}!");
}
