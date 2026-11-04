using Autodesk.Revit.DB;

namespace CQEING.Viguetas.Revit;

/// <summary>
/// Puntos de la geometría de un elemento y paso a mm, como en el lector del add-in de vigas: el ancho y el centro se miden
/// en la geometría, no en parámetros de la familia (cada familia los llama distinto).
/// </summary>
internal static class Medidas
{
    /// <summary>Puntos de las aristas de los sólidos del elemento (también dentro de instancias de familia).</summary>
    public static List<XYZ> Puntos(Element e) => Puntos(e.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).ToList();

    public static double Mm(double piesInternos) => UnitUtils.ConvertFromInternalUnits(piesInternos, UnitTypeId.Millimeters);

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
}
