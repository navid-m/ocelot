using Ocelot.Meta.Test;
using Ocelot.Server;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new HTTPServer("127.0.0.1", 8080);
        server.RegisterRoutes<ExampleController>();
        server.UseStaticFiles("Meta/Test/Static");
        await server.StartAsync();
    }
}
