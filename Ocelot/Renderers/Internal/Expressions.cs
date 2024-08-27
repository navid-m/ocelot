using System.Text.RegularExpressions;

namespace Ocelot.Renderers.Internal;

internal static partial class Expressions
{
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    public static partial Regex TemplateValueRegex();
}
