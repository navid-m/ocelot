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
            app.UseStaticFiles("Meta/Test/Static");
            app.UseTemplatePath("Meta/Test/Views");
            Assert.Pass();
        }
    }
}
