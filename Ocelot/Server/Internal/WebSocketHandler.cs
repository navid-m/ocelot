using System.Net.WebSockets;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ocelot.Server.Internal
{
    internal sealed class WebSocketHandler
    {
        private readonly MethodInfo _method;
        private readonly object _instance;
        private readonly Regex _routeRegex;

        public WebSocketHandler(string routePattern, MethodInfo method, object instance)
        {
            _method = method;
            _instance = instance;
            _routeRegex = new Regex($"^{routePattern}$", RegexOptions.Compiled);
        }

        public bool IsMatch(string route) => _routeRegex.IsMatch(route);

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            await (Task)_method.Invoke(_instance, [webSocket])!;
        }
    }
}
