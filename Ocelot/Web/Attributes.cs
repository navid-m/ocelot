namespace Ocelot.Web;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GetAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WsAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
