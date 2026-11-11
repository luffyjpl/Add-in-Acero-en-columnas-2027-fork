using Autodesk.Revit.DB;
using CQEING.AreaUtil.Calculo;

namespace CQEING.AreaUtil.Revit;

/// <summary>Los estilos «CQ …» como subcategorías de Líneas (decisión 2 del spec). Medido en la tarea 7, paso 1.</summary>
internal static class EstilosRevit
{
    /// <summary>Crea o actualiza cada estilo con su color y grosor. Dentro de una transacción abierta.</summary>
    public static Dictionary<string, GraphicsStyle> Asegurar(Document doc, IReadOnlyList<EstiloLinea> estilos)
    {
        Category lineas = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        var porClave = new Dictionary<string, GraphicsStyle>();
        foreach (EstiloLinea e in estilos)
        {
            Category sub = lineas.SubCategories.Contains(e.Nombre)
                ? lineas.SubCategories.get_Item(e.Nombre)
                : doc.Settings.Categories.NewSubcategory(lineas, e.Nombre);
            sub.LineColor = DeHex(e.Color);
            sub.SetLineWeight(Math.Clamp(e.Grosor, 1, 16), GraphicsStyleType.Projection);
            porClave[e.Clave] = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
        }
        return porClave;
    }

    /// <summary>«#RRGGBB»; si no se entiende, gris.</summary>
    private static Color DeHex(string hex)
    {
        string h = hex.TrimStart('#');
        return h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out int v)
            ? new Color((byte)(v >> 16), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF))
            : new Color(128, 128, 128);
    }
}
