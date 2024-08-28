using System.Net.WebSockets;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ocelot.Server.Internal
{
    internal sealed class WebSocketHandler(string routePattern, MethodInfo method, object instance)
    {
        private readonly MethodInfo _method = method;
        private readonly object _instance = instance;
        private readonly Regex _routeRegex = new($"^{routePattern}$", RegexOptions.Compiled);

        public bool IsMatch(string route) => _routeRegex.IsMatch(route);

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            await (Task)_method.Invoke(_instance, [webSocket])!;
        }
    }
}
