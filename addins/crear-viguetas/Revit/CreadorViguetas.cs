using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Viguetas.Calculo;

namespace CQEING.Viguetas.Revit;

/// <summary>
/// Crea y quita en Revit las viguetas de un panel (#952). Cada operación es una transacción: Ctrl+Z la deshace entera.
/// </summary>
internal static class CreadorViguetas
{
    /// <summary>
    /// Crea una vigueta por línea (<see cref="PropuestaViguetas.Lineas"/>, de eje a eje de viga) con
    /// <paramref name="tipo"/>, en <paramref name="nivel"/> y a la cota de las vigas del piso. Antes quita las que el
    /// add-in ya había creado en esos paneles (<paramref name="anteriores"/>), así repetir no las duplica, y recorta las
    /// viguetas del modelo que el usuario marcó (<paramref name="recortes"/>, decisiones 19 y 21). Devuelve cuántas creó y
    /// cuántas del modelo recortó.
    /// </summary>
    public static (int Creadas, int Recortadas) Crear(Document doc, Level nivel, FamilySymbol tipo, IEnumerable<(Punto Desde, Punto Hasta)> lineas,
        IEnumerable<ElementId> anteriores, IEnumerable<(ElementId Id, List<(double Desde, double Hasta)> Restos)> recortes)
    {
        double z = LectorPiso.CotaLineas(doc, nivel);
        double minimo = doc.Application.ShortCurveTolerance;
        using var t = new Transaction(doc, "CQEING: crear viguetas");
        t.Start();
        Borrar(doc, anteriores);
        int recortadas = recortes.Count(r => Recortar(doc, r.Id, r.Restos));
        if (!tipo.IsActive)
            tipo.Activate();
        int creadas = 0;
        foreach ((Punto desde, Punto hasta) in lineas)
        {
            XYZ a = Pies(desde, z), b = Pies(hasta, z);
            if (a.DistanceTo(b) <= minimo)
                continue;
            FamilyInstance f = doc.Create.NewFamilyInstance(Line.CreateBound(a, b), tipo, nivel, StructuralType.Beam);
            f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(LectorPiso.Marca);
            creadas++;
        }
        t.Commit();
        return (creadas, recortadas);
    }

    /// <summary>Quita las viguetas que creó el add-in (solo esas: se comprueba la marca). Devuelve cuántas quitó.</summary>
    public static int Quitar(Document doc, IEnumerable<ElementId> ids)
    {
        using var t = new Transaction(doc, "CQEING: quitar viguetas");
        t.Start();
        int quitadas = Borrar(doc, ids);
        t.Commit();
        return quitadas;
    }

    /// <summary>
    /// Deja de la vigueta del modelo solo sus <paramref name="restos"/> (fracciones de su línea de ubicación, decisión 21):
    /// el elemento se acorta al primero y cada uno de los demás va en una copia del mismo elemento. Sin restos, se borra.
    /// Solo toca armazón estructural.
    /// </summary>
    private static bool Recortar(Document doc, ElementId id, List<(double Desde, double Hasta)> restos)
    {
        if (doc.GetElement(id) is not FamilyInstance f || f.Category?.Id.Value != (long)BuiltInCategory.OST_StructuralFraming
            || f.Location is not LocationCurve { Curve: Line linea } ubicacion)
            return false;
        if (restos.Count == 0)
        {
            doc.Delete(id);
            return true;
        }
        XYZ p0 = linea.GetEndPoint(0), p1 = linea.GetEndPoint(1);
        Line Tramo((double Desde, double Hasta) t) => Line.CreateBound(p0 + (p1 - p0) * t.Desde, p0 + (p1 - p0) * t.Hasta);
        foreach ((double, double) resto in restos.Skip(1))
            if (doc.GetElement(ElementTransformUtils.CopyElement(doc, id, XYZ.Zero).First()).Location is LocationCurve copia)
                copia.Curve = Tramo(resto);
        ubicacion.Curve = Tramo(restos[0]);
        return true;
    }

    private static int Borrar(Document doc, IEnumerable<ElementId> ids)
    {
        List<ElementId> nuestras = ids.Where(id => doc.GetElement(id) is Element e && LectorPiso.EsCreada(e)).ToList();
        if (nuestras.Count > 0)
            doc.Delete(nuestras);
        return nuestras.Count;
    }

    private static XYZ Pies(Punto p, double z) =>
        new(UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Millimeters), UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Millimeters), z);
}
