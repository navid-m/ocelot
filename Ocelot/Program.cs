using Ocelot.Server;
using Ocelot.Test;

class Program
{
    static async Task Main(string[] args)
    {
        var app = new HTTPServer("127.0.0.1", 5001);
        app.RegisterRoutes<ExampleController>();
        await app.StartAsync();
    }
}
