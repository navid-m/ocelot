using Ocelot.Renderers.Internal;
using Ocelot.Renderers.Models;
using Ocelot.Reports;
using Ocelot.Responses;

namespace Ocelot.Renderers;

internal static class ViewRenderer
{
    private static string TemplatesPath;
    private static string TemplateExtension;

    static ViewRenderer()
    {
        TemplatesPath = "Views";
        TemplateExtension = "blade";
    }

    public static View Render(ViewModel model, string viewPath)
    {
        string fullViewPath = Path.Join(TemplatesPath, viewPath + $".{TemplateExtension}");
        if (!File.Exists(fullViewPath))
        {
            Logger.LogIssue("View path does not exist.");
        }
        if (BladeParser.TryParse(File.ReadAllText(fullViewPath), out var template, out var error))
        {
            return new View(template.Render(new TemplateContext(model)));
        }
        Logger.LogIssue(errorMessage: $"Issue parsing template: {error}", fatal: true);
        return new View(string.Empty);
    }

    public static void SetTemplatesPath(string path) => TemplatesPath = path;

    public static void SetTemplateExtension(string ext) => TemplateExtension = ext;
}
