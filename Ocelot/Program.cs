using Ocelot.Server;
using Ocelot.Test;

class Program
{
    static async Task Main(string[] args)
    {
        var framework = new OcelotServer("127.0.0.1", 5001);
        framework.RegisterRoutes<ExampleController>();
        await framework.StartAsync();
    }
}
