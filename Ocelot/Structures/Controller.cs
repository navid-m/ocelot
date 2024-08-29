using Ocelot.Rendering;
using Ocelot.Rendering.Models;
using Ocelot.Responses;

namespace Ocelot.Structures;

public class Controller
{
    public static View Render(ViewModel model, string viewPath)
    {
        return ViewRenderer.Render(model, viewPath);
    }

    public static View Render(string viewPath)
    {
        return ViewRenderer.Render([], viewPath);
    }
}
