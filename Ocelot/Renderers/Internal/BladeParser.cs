using Ocelot.Renderers.Models;

namespace Ocelot.Renderers.Internal;

internal partial class BladeParser
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
        if (!Expressions.PlaceholderRegex().IsMatch(template))
        {
            error = "No placeholders found in the template.";
            return false;
        }
        if (error != null)
        {
            Reports.StatusLogger.LogFailure(error, Reports.Data.Location.RENDERER);
        }
        templateObj = new Template(template);
        return true;
    }
}
