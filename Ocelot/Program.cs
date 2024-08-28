using Ocelot.Meta.Test;
using Ocelot.Renderers;
using Ocelot.Server;

class Program
{
    static async Task Main()
    {
        var app = new HTTPServer("127.0.0.1", 8080);

        app.RegisterRoutes<HomeController>();
        app.UseStaticFiles("Meta/Test/Static");
        HTTPServer.UseTemplatePath("Meta/Test/Views");

        await app.StartAsync();
    }
}
