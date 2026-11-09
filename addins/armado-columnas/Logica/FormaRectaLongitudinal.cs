namespace practicacq.Logica;

using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

/// <summary>
/// Que el longitudinal no dependa de las formas del modelo (decisión 5 de
/// docs/planes/2026-11-07-estribos-centrados-y-fallas-santiago.md).
///
/// <c>CreateFromCurves(useExistingShapeIfPossible)</c> toma la primera forma
/// del modelo que encaje. En nuestra plantilla es la «00»; en otra puede ser
/// cualquiera, y una forma que Revit eligió solo ya rompió los empalmes en el
/// #176. Acá se elige la forma recta explícitamente.
///
/// Con "Incluir ganchos en la definición de la forma"
/// (<see cref="ReinforcementSettings.RebarShapeDefinesHooks"/>) apagado, como
/// en nuestros modelos, el gancho no es parte de la forma: se pone aparte, con
/// <c>BarTerminationsData</c>. Encendido no se elige nada y Revit sigue
/// eligiendo como antes (decisión 9: el ajuste del modelo no se toca).
/// </summary>
public static class FormaRectaLongitudinal
{
    /// <summary>
    /// La forma recta para las longitudinales: un solo tramo recto, estilo
    /// estándar, sin tratamiento de extremos; primero la «00» de la plantilla
    /// (o «M_00»), que es la que Revit elige en nuestros modelos. Nunca una
    /// «CQ …» propia (#176). Null si el modelo define los ganchos en la forma
    /// (ahí la forma tendría que llevar el gancho) o si no hay ninguna.
    /// </summary>
    public static RebarShape? Buscar(Document doc)
    {
        try
        {
            if (ReinforcementSettings.GetReinforcementSettings(doc).RebarShapeDefinesHooks) return null;

            return new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>()
                .Where(EsRecta)
                .OrderBy(f => f.Name == "00" ? 0 : f.Name == "M_00" ? 1 : 2)
                .ThenBy(f => f.Name, StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool EsRecta(RebarShape forma)
    {
        try
        {
            return forma.SimpleLine && forma.RebarStyle == RebarStyle.Standard && !forma.HasEndTreatment() &&
                   !forma.Name.StartsWith("CQ ", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
