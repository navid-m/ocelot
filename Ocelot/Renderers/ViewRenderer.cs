using Ocelot.Renderers.Internal;
using Ocelot.Renderers.Models;
using Ocelot.Reports;
using Ocelot.Responses;

namespace Ocelot.Renderers;

public static class ViewRenderer
{
    private static string templatesPath;

    static ViewRenderer()
    {
        templatesPath = "Views";
    }

    public static void SetTemplatePath(string path)
    {
        templatesPath = path;
    }

    public static HTMLResponse Render(object model, string viewPath)
    {
        string fullViewPath = Path.Join(templatesPath, viewPath);
        if (!File.Exists(fullViewPath))
        {
            Logger.LogIssue("View path does not exist.");
        }
        if (BladeParser.TryParse(File.ReadAllText(fullViewPath), out var template, out var error))
        {
            return new HTMLResponse(template.Render(new TemplateContext(model)));
        }
        Logger.LogIssue(errorMessage: $"Issue parsing template: {error}", fatal: true);
        return new HTMLResponse(string.Empty);
    }
}
