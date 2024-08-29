# Ocelot.Web

Dependency-free, minimal MVC web framework designed for .NET, optimized for Ahead-Of-Time (AOT) compilation. 

____

## Why

ASP .NET Core does not support MVC with AOT.

## Running an app

To run a web application:

1. Initialize the `HttpServer` with your desired host and port.
2. Register your controller routes using `RegisterRoutes`.
3. Optionally, configure static file serving and template paths.

```csharp
using Ocelot.Rendering.Models;
using Ocelot.Responses;
using Ocelot.Structures;
using Ocelot.Web;

var app = new HttpServer("127.0.0.1", 8080);

app.RegisterRoutes<HomeController>();
app.UseStaticFiles("wwwroot");
app.UseTemplatePath("Views");

app.Run();
```



## Templating

A no logic, variable only template engine is included for HTML views.

Passing a ViewModel from the controller like this:

```csharp
// ...
public class HomeController : Controller
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
// ...
```

Its values can then be accessed from index.html in the Views directory like so:

```
<html>
{{Firstname}} {{Lastname}}
</html>
```

Obviously, other more convoluted templating engines like Liquid, etc... can also be used.
Though, they will void AOT compatibility.


## Project Structure

- **Controllers**: Define routes and logic in controllers by inheriting from `Ocelot.Web.Controller`.
- **Views**: Use no-logic templates to render HTML, stored in the path specified with `UseTemplatePath`.
- **Static Files**: Serve static content like CSS, JS, images, etc using `UseStaticFiles`.

## Example Usage

```csharp
using Ocelot.Rendering.Models;
using Ocelot.Responses;
using Ocelot.Structures;
using Ocelot.Web;

namespace Ocelot.Test
{
    public class HomeController : Controller
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
        public static Text Submit(HttpRequest request) => new($"Received: {request.Body}");

        [Get("/json")]
        public static Json SerialExample() => new("{\"message\": \"This is a JSON response\"}");

        [Get("/about")]
        public static Text About() => new("This is the about page.");

        [Get("/increment/{number}")]
        public static Text Increment(string value) => new($"Incremented: {int.Parse(value) + 1}");

        [Get("/greet/{entity}/{name}")]
        public static Text Greet(string entity, string name) => new($"Hello {name}, the {entity}");
    }

    public class MainTests
    {
        [Test]
        public void ScaffoldingTest()
        {
            var app = new HttpServer("127.0.0.1", 8080);
            app.RegisterRoutes<HomeController>();
            app.UseStaticFiles("Static");
            app.UseTemplatePath("Views");
            Assert.Pass();
        }
    }
}
```

