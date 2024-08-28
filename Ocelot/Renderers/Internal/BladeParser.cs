using Ocelot.Renderers.Models;

namespace Ocelot.Renderers.Internal;

internal sealed partial class BladeParser
{
    public static bool TryParse(string template, out Template templateObj, out string? error)
    {
        templateObj = new Template(string.Empty);
        error = null;
        if (string.IsNullOrEmpty(template))
        {
            error = "Template is empty.";
            return false;
        }
        templateObj = new Template(template);
        return true;
    }
}
