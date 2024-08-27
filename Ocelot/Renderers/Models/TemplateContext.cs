namespace Ocelot.Renderers.Models;

internal class TemplateContext
{
    private readonly Dictionary<string, object> templateVariables;

    public TemplateContext(object model)
    {
        templateVariables = [];
        foreach (var prop in model.GetType().GetProperties())
        {
            var val = prop.GetValue(model);
            if (val != null)
            {
                templateVariables[prop.Name] = val;
            }
        }
    }

    public bool TryGetValue(string key, out object? value)
    {
        return templateVariables.TryGetValue(key, out value);
    }
}
