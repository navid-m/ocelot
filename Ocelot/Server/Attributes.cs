namespace Ocelot.Server;

[AttributeUsage(AttributeTargets.Method)]
public class GetAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}

[AttributeUsage(AttributeTargets.Method)]
public class PostAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
