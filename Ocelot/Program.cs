using Ocelot.Meta.Test;
using Ocelot.Server;

class Program
{
    static async Task Main()
    {
        var app = new HTTPServer("127.0.0.1", 8080);

        app.RegisterRoutes<HomeController>();
        app.UseStaticFiles("Meta/Test/Static");
        app.UseTemplatePath("Meta/Test/Views");

        await app.StartAsync();
    }
}
