using Ocelot.Renderers.Internal;

namespace Ocelot.Renderers.Models;

internal class Template(string template)
{
    private readonly string _template = template;

    public string Render(TemplateContext context)
    {
        return Expressions
            .PlaceholderRegex()
            .Replace(
                _template,
                match =>
                {
                    var variableName = match.Groups[1].Value;
                    if (context.TryGetValue(variableName, out var value))
                    {
                        if (value != null)
                        {
                            return value.ToString() ?? "";
                        }
                    }
                    return match.Value;
                }
            );
    }
}
