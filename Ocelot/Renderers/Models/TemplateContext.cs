namespace Ocelot.Renderers.Models;

internal class TemplateContext
{
    private readonly Dictionary<string, object> _variables;

    public TemplateContext(object model)
    {
        _variables = [];
        foreach (var prop in model.GetType().GetProperties())
        {
            var val = prop.GetValue(model);
            if (val != null)
            {
                _variables[prop.Name] = val;
            }
        }
    }

    public bool TryGetValue(string key, out object? value)
    {
        return _variables.TryGetValue(key, out value);
    }
}
