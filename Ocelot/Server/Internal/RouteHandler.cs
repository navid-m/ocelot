using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ocelot.Responses;
using Ocelot.Server.Exceptions;

namespace Ocelot.Server.Internal;

internal sealed partial class RouteHandler
{
    private readonly MethodInfo _method;
    private readonly object _instance;
    private readonly bool _expectsRequest;
    private readonly Regex _routeRegex;
    private readonly string[] _parameterNames;

    public RouteHandler(
        string routePattern,
        MethodInfo method,
        object instance,
        bool expectsRequest = false
    )
    {
        _method = method;
        _instance = instance;
        _expectsRequest = expectsRequest;
        (_routeRegex, _parameterNames) = BuildRouteRegex(routePattern);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (Regex, string[]) BuildRouteRegex(string routePattern)
    {
        var parameterNames = new List<string>();
        var pattern =
            "^"
            + RouteBuildingRegex()
                .Replace(
                    routePattern,
                    match =>
                    {
                        parameterNames.Add(match.Groups[1].Value);
                        return @"([^/]+)";
                    }
                )
            + "$";
        return (new Regex(pattern), [.. parameterNames]);
    }

    public bool IsMatch(string route) => _routeRegex.IsMatch(route);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public byte[] Invoke(HttpRequest request)
    {
        Match match = _routeRegex.Match(request.Route!);

        if (!match.Success)
        {
            throw new InvalidRouteException("Route did not match.");
        }
        var parameterValues = new object[_parameterNames.Length + (_expectsRequest ? 1 : 0)];
        for (int i = 0; i < _parameterNames.Length; i++)
        {
            parameterValues[_expectsRequest ? i + 1 : i] = match.Groups[i + 1].Value;
        }
        if (_expectsRequest)
        {
            parameterValues[0] = request;
        }
        try
        {
            return ResponseBuilder.GenerateHttpResponse(
                (Response)_method.Invoke(_instance, parameterValues)!
            );
        }
        catch (Exception e)
        {
            throw new ResponseGenerationException($"Issue generating HTTP response: {e.Message}");
        }
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex RouteBuildingRegex();
}
