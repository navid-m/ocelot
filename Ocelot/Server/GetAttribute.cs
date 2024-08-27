namespace Ocelot.Server;

[AttributeUsage(AttributeTargets.Method)]
public class GetAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
