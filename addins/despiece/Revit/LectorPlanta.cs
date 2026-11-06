using Autodesk.Revit.DB;
using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit;

/// <summary>
/// Lee del modelo lo que se dibuja en la planta del tablero (#285, #286; decisión 7 del plan
/// docs/planes/2026-11-05-reunion-despiece-287.md): los ejes, las vigas del nivel y las columnas que lo tocan. Como el
/// tablero de vigas (<c>acero-vigas/Revit/LectorModelo.cs</c>), no exporta ninguna vista ni toca el modelo: así no puede
/// salir un CAD ni una anotación.
/// </summary>
internal static class LectorPlanta
{
    private const double MetrosPorPie = 0.3048;

    /// <summary>Lo que una columna puede quedarse corta o pasarse de la cota del nivel y seguir en él.</summary>
    private const double Tolerancia = 0.05 / MetrosPorPie;

    public static PlantaDibujada Leer(Document doc, Level nivel)
    {
        List<EjeLeido> ejes = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>()
            .Where(g => g.Curve is Line)
            .Select(g => new EjeLeido(g.Name, Plano(g.Curve.GetEndPoint(0)), Plano(g.Curve.GetEndPoint(1))))
            .ToList();

        var elementos = new List<ElementoEnPlanta>();
        // El mismo criterio que LectorDespiece.Plantas: la viga es del nivel de referencia que tiene.
        foreach (FamilyInstance viga in Instancias(doc, BuiltInCategory.OST_StructuralFraming))
        {
            if (viga.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() == nivel.Id)
                elementos.Add(new ElementoEnPlanta(viga.Id.ToString(), false, Puntos(viga).Select(Plano).ToList()));
        }
        double cota = nivel.Elevation;
        foreach (FamilyInstance columna in Instancias(doc, BuiltInCategory.OST_StructuralColumns))
        {
            List<XYZ> puntos = Puntos(columna);
            if (puntos.Count == 0 || puntos.Min(p => p.Z) > cota + Tolerancia || puntos.Max(p => p.Z) < cota - Tolerancia)
                continue;
            elementos.Add(new ElementoEnPlanta(columna.Id.ToString(), true, puntos.Select(Plano).ToList()));
        }
        Registro.Paso($"planta: {nivel.Name}, {ejes.Count} ejes, {elementos.Count(e => !e.EsColumna)} vigas, {elementos.Count(e => e.EsColumna)} columnas");
        return ArmadorPlanta.Armar(ejes, elementos);
    }

    private static IEnumerable<FamilyInstance> Instancias(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType().OfType<FamilyInstance>();

    /// <summary>Los puntos de las aristas de sus sólidos, también dentro de la familia.</summary>
    private static List<XYZ> Puntos(Element e) => Puntos(e.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).ToList();

    private static IEnumerable<XYZ> Puntos(GeometryElement? geometria)
    {
        if (geometria is null)
            yield break;
        foreach (GeometryObject objeto in geometria)
        {
            if (objeto is Solid solido && solido.Volume > 0)
            {
                foreach (Edge arista in solido.Edges)
                    foreach (XYZ p in arista.Tessellate())
                        yield return p;
            }
            else if (objeto is GeometryInstance instancia)
            {
                foreach (XYZ p in Puntos(instancia.GetInstanceGeometry()))
                    yield return p;
            }
        }
    }

    private static PuntoModelo Plano(XYZ p) => new(p.X * MetrosPorPie, p.Y * MetrosPorPie);
}
