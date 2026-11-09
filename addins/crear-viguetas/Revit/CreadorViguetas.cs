using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Viguetas.Calculo;

namespace CQEING.Viguetas.Revit;

/// <summary>Lo que se hace en un piso al crear (decisión 34): las líneas, las creadas a recortar y las demás viguetas a recortar.</summary>
internal sealed record TrabajoViguetas(Level Nivel, IReadOnlyList<(Punto Desde, Punto Hasta)> Lineas,
    IReadOnlyList<(ElementId Id, List<(double Desde, double Hasta)> Restos)> Anteriores,
    IReadOnlyList<(ElementId Id, List<(double Desde, double Hasta)> Restos)> Recortes);

/// <summary>
/// Crea y quita en Revit las viguetas de un panel (#952). Cada operación es una transacción: Ctrl+Z la deshace entera.
/// </summary>
internal static class CreadorViguetas
{
    /// <summary>
    /// Crea, piso por piso (<paramref name="trabajos"/>, decisión 34) y en una sola transacción, una vigueta por línea
    /// (<see cref="PropuestaViguetas.Lineas"/>, de eje a eje de viga) con <paramref name="tipo"/>, a la cota de las vigas
    /// de cada piso. Antes quita las que el add-in ya había creado ahí (solo su tramo dentro de lo elegido: decisión 32),
    /// así repetir no las duplica, y recorta las viguetas del modelo que el usuario marcó (decisiones 19 y 21). Devuelve,
    /// por piso, cuántas creó y cuántas del modelo recortó.
    /// </summary>
    public static List<(int Creadas, int Recortadas)> Crear(Document doc, FamilySymbol tipo, IReadOnlyList<TrabajoViguetas> trabajos)
    {
        double minimo = doc.Application.ShortCurveTolerance;
        using var t = new Transaction(doc, "CQEING: crear viguetas");
        t.Start();
        if (!tipo.IsActive)
            tipo.Activate();
        var resultados = new List<(int Creadas, int Recortadas)>();
        foreach (TrabajoViguetas trabajo in trabajos)
        {
            double z = LectorPiso.CotaLineas(doc, trabajo.Nivel);
            RecortarCreadas(doc, trabajo.Anteriores);
            int recortadas = trabajo.Recortes.Count(r => Recortar(doc, r.Id, r.Restos));
            int creadas = 0;
            foreach ((Punto desde, Punto hasta) in trabajo.Lineas)
            {
                XYZ a = Pies(desde, z), b = Pies(hasta, z);
                if (a.DistanceTo(b) <= minimo)
                    continue;
                FamilyInstance f = doc.Create.NewFamilyInstance(Line.CreateBound(a, b), tipo, trabajo.Nivel, StructuralType.Beam);
                f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(LectorPiso.Marca);
                creadas++;
            }
            resultados.Add((creadas, recortadas));
        }
        t.Commit();
        return resultados;
    }

    /// <summary>
    /// Quita las viguetas que creó el add-in en los paneles pedidos: solo su tramo dentro de ellos (decisión 32). Devuelve
    /// cuántas tocó.
    /// </summary>
    public static int Quitar(Document doc, IEnumerable<(ElementId Id, List<(double Desde, double Hasta)> Restos)> recortes)
    {
        using var t = new Transaction(doc, "CQEING: quitar viguetas");
        t.Start();
        int quitadas = RecortarCreadas(doc, recortes);
        t.Commit();
        return quitadas;
    }

    /// <summary>
    /// Decisión 30: corta las viguetas que cruzan un panel que se vuelve vacío. Una sola transacción: Ctrl+Z la deshace
    /// entera. Devuelve cuántas tocó.
    /// </summary>
    public static int Vaciar(Document doc, IEnumerable<(ElementId Id, List<(double Desde, double Hasta)> Restos)> recortes)
    {
        using var t = new Transaction(doc, "CQEING: vacío en panel");
        t.Start();
        int cortadas = recortes.Count(r => Recortar(doc, r.Id, r.Restos));
        t.Commit();
        return cortadas;
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

    /// <summary>Recorta solo las que creó el add-in: se comprueba la marca.</summary>
    private static int RecortarCreadas(Document doc, IEnumerable<(ElementId Id, List<(double Desde, double Hasta)> Restos)> recortes) =>
        recortes.Count(r => doc.GetElement(r.Id) is Element e && LectorPiso.EsCreada(e) && Recortar(doc, r.Id, r.Restos));

    private static XYZ Pies(Punto p, double z) =>
        new(UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Millimeters), UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Millimeters), z);
}
