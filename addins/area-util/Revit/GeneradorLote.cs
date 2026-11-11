using Autodesk.Revit.DB;
using CQEING.AreaUtil.Calculo;

namespace CQEING.AreaUtil.Revit;

/// <summary>
/// Dibuja en Revit lo calculado, en una sola transacción («Área útil del lote»), así Ctrl+Z lo deshace de una vez.
/// Antes borra lo que generó el add-in (decisión 12). Coordenadas: decisión 8 del spec, medida en la tarea 7.
/// </summary>
internal static class GeneradorLote
{
    /// <summary>Revit pierde precisión lejos del origen interno; más allá de esto se avisa y no se genera.</summary>
    private const double DistanciaMaximaM = 30_000;

    /// <summary>Devuelve las líneas creadas, para mostrarlas (decisión 16).</summary>
    public static List<ElementId> Generar(Document doc, ResultadoLote r, Level nivel, bool compartidas, IReadOnlyList<EstiloLinea> estilos)
    {
        // Medido en Revit 2026.5 (hallazgo 9 del plan): GetTotalTransform lleva de compartidas a internas, también con el
        // norte girado. Su inversa dejaba el lote a kilómetros.
        Transform aInterno = compartidas ? doc.ActiveProjectLocation.GetTotalTransform() : Transform.Identity;
        XYZ Interno(Punto p) => aInterno.OfPoint(new XYZ(Pies(p.X), Pies(p.Y), 0));

        if (r.Trazos.SelectMany(x => new[] { x.A, x.B }).Select(Interno).Any(q => Metros(Math.Sqrt(q.X * q.X + q.Y * q.Y)) > DistanciaMaximaM))
            throw new InvalidOperationException(
                "El lote queda a más de 30 km del origen del proyecto: revise el punto de reconocimiento o use «Coordenadas del proyecto».");

        using var tx = new Transaction(doc, "Área útil del lote");
        tx.Start();
        Dictionary<string, GraphicsStyle> estilo = EstilosRevit.Asegurar(doc, estilos);
        MarcaAreaUtil.BorrarTodas(doc);
        SketchPlane plano = SketchPlane.Create(doc, nivel.Id);
        double z = plano.GetPlane().Origin.Z;
        double corta = doc.Application.ShortCurveTolerance;
        var creadas = new List<ElementId>();
        foreach (Trazo trazo in r.Trazos)
        {
            XYZ a = Interno(trazo.A), b = Interno(trazo.B);
            var inicio = new XYZ(a.X, a.Y, z);
            var fin = new XYZ(b.X, b.Y, z);
            if (inicio.DistanceTo(fin) < corta)
                continue;
            ModelCurve linea = doc.Create.NewModelCurve(Line.CreateBound(inicio, fin), plano);
            linea.LineStyle = estilo[trazo.Estilo];
            MarcaAreaUtil.Marcar(linea, trazo.Estilo);
            creadas.Add(linea.Id);
        }
        tx.Commit();
        return creadas;
    }

    private static double Pies(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);

    private static double Metros(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Meters);
}
