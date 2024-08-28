namespace Ocelot.Renderers.Models;

internal sealed class TemplateContext
{
    private readonly Dictionary<string, object> templateVariables;

    public TemplateContext(ViewModel model)
    {
        templateVariables = [];
        foreach (var key in model.Keys)
        {
            // TODO: Add type processing here.
            templateVariables[key] = model[key];
        }
    }

    public bool TryGetValue(string key, out object? value)
    {
        return templateVariables.TryGetValue(key, out value);
    }
}
