using Ocelot.Server;

namespace Ocelot.Test;

public class ExampleController
{
    [Get("/")]
    public static string Index()
    {
        return "Hello, World!";
    }

    [Get("/about")]
    public static string About()
    {
        return "This is the about page.";
    }
}
