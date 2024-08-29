using System.Text.RegularExpressions;

namespace Ocelot.Rendering.Internal;

internal static partial class Expressions
{
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    public static partial Regex TemplateValueRegex();
}
