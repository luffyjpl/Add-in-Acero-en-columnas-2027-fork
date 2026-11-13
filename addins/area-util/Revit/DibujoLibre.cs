using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CQEING.AreaUtil.Calculo;
using CQEING.AreaUtil.Tablero;

namespace CQEING.AreaUtil.Revit;

/// <summary>Cómo terminó el dibujo y los puntos dibujados, en pies internos.</summary>
internal sealed record ResultadoDibujo(bool Terminado, IReadOnlyList<XYZ> Puntos);

/// <summary>
/// El dibujo libre del lote (decisión 21): un clic por esquina con enganches; Esc o un clic sobre el primer punto
/// termina. Cada tramo es una línea de modelo temporal dentro de un TransactionGroup que se deshace al final, así no
/// queda nada en el modelo ni en Ctrl+Z. Corre dentro del contexto de la API, en una planta activa.
/// </summary>
internal static class DibujoLibre
{
    private const ObjectSnapTypes Enganches =
        ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest;

    public static ResultadoDibujo Dibujar(UIDocument uidoc, BarraDibujo barra, Action<string> anotar)
    {
        Document doc = uidoc.Document;
        if (uidoc.ActiveView is not ViewPlan vista)
            throw new InvalidOperationException("El dibujo libre necesita una planta activa.");
        // La cuenta (cierre, repetidos, deshacer) es TrazadoLote, con pruebas; aquí solo los clics y las líneas.
        var trazado = new TrazadoLote();
        var puntos = new List<XYZ>();
        var tramos = new List<ElementId>();
        using var grupo = new TransactionGroup(doc, "Dibujo del lote");
        grupo.Start();
        try
        {
            SketchPlane plano = Plano(doc, vista);
            while (true)
            {
                barra.Mostrar(puntos.Count);
                XYZ p;
                try
                {
                    p = uidoc.Selection.PickPoint(Enganches, Mensaje(puntos.Count));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    AccionBarra accion = barra.Tomar();
                    anotar($"Clic cortado (Esc); acción de la barra: {accion}; puntos: {puntos.Count}");
                    if (accion == AccionBarra.Deshacer)
                    {
                        if (trazado.Deshacer())
                            Deshacer(doc, puntos, tramos);
                        continue;
                    }
                    if (accion == AccionBarra.Cancelar)
                        return new ResultadoDibujo(false, []);
                    return new ResultadoDibujo(trazado.PuedeTerminar, puntos);
                }
                switch (trazado.Agregar(new Punto(Metros(p.X), Metros(p.Y))))
                {
                    case PasoTrazado.Cerrado:
                        anotar($"Clic sobre el primer punto: lote cerrado con {puntos.Count} puntos");
                        return new ResultadoDibujo(true, puntos);
                    case PasoTrazado.Repetido:
                        anotar("Clic sobre el punto anterior: se ignora");
                        continue;
                }
                if (puntos.Count > 0)
                    tramos.Add(Tramo(doc, plano, puntos[^1], p));
                puntos.Add(p);
                anotar($"Punto {puntos.Count}: ({Metros(p.X):0.###}, {Metros(p.Y):0.###}) m");
                uidoc.RefreshActiveView();
            }
        }
        finally
        {
            // Las líneas temporales y el plano se van con el grupo: el modelo queda como estaba.
            grupo.RollBack();
        }
    }

    private static string Mensaje(int puntos) => puntos == 0
        ? "Área útil del lote: clic en la primera esquina del lote (Esc cancela)."
        : puntos < 3
            ? $"Área útil del lote: clic en la esquina {puntos + 1} (Esc cancela; hacen falta 3 o más)."
            : $"Área útil del lote: clic en la esquina {puntos + 1}, o en la primera para cerrar (Esc termina).";

    private static SketchPlane Plano(Document doc, ViewPlan vista)
    {
        if (vista.SketchPlane is { } existente)
            return existente;
        using var t = new Transaction(doc, "Plano del dibujo");
        t.Start();
        SketchPlane plano = SketchPlane.Create(doc, vista.GenLevel.Id);
        t.Commit();
        return plano;
    }

    private static ElementId Tramo(Document doc, SketchPlane plano, XYZ a, XYZ b)
    {
        double z = plano.GetPlane().Origin.Z;
        using var t = new Transaction(doc, "Tramo del dibujo");
        t.Start();
        ModelCurve linea = doc.Create.NewModelCurve(Line.CreateBound(new XYZ(a.X, a.Y, z), new XYZ(b.X, b.Y, z)), plano);
        t.Commit();
        return linea.Id;
    }

    private static void Deshacer(Document doc, List<XYZ> puntos, List<ElementId> tramos)
    {
        puntos.RemoveAt(puntos.Count - 1);
        if (tramos.Count == 0)
            return;
        using var t = new Transaction(doc, "Deshacer tramo");
        t.Start();
        doc.Delete(tramos[^1]);
        t.Commit();
        tramos.RemoveAt(tramos.Count - 1);
    }

    private static double Metros(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Meters);
}
