using Autodesk.Revit.DB;

namespace CQEING.Despiece.Revit;

/// <summary>
/// Lo que entra al modelo desde afuera: CAD importados o vinculados, vínculos de Revit y nubes de puntos (#263). El
/// despiece trabaja solo con elementos del modelo propio, así que nada de esto tiene que verse: en modelos de otros
/// ingenieros un CAD tapaba la planta del tablero.
/// </summary>
internal static class Importados
{
    /// <summary>
    /// Oculta en la vista todo lo que entra de afuera. Se oculta por elemento y no por categoría, porque la categoría
    /// de un CAD es la del nombre de su archivo. Hace falta aunque la vista oculte el resto: las vistas del despiece
    /// quitan la plantilla, y a veces era la plantilla la que escondía el CAD.
    /// </summary>
    public static void Ocultar(Document doc, View vista)
    {
        List<ElementId> ocultar = new[] { typeof(ImportInstance), typeof(RevitLinkInstance), typeof(PointCloudInstance) }
            .SelectMany(clase => new FilteredElementCollector(doc, vista.Id).OfClass(clase))
            .Where(e => e.CanBeHidden(vista))
            .Select(e => e.Id).ToList();
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
    }
}
