using Ocelot.Renderers.Internal;

namespace Ocelot.Renderers.Models;

internal sealed class Template(string template)
{
    private readonly string _template = template;

    public string Render(TemplateContext context)
    {
        return Expressions
            .TemplateValueRegex()
            .Replace(
                _template,
                match =>
                {
                    if (context.TryGetValue(match.Groups[1].Value, out var value))
                    {
                        if (value != null)
                        {
                            return value.ToString() ?? string.Empty;
                        }
                    }
                    return match.Value;
                }
            );
    }
}
