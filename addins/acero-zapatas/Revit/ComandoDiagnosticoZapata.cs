#if DEBUG
using System.IO;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// Diagnóstico temporal (paso 5 del plan de #246): mide cómo se comporta Revit con una zapata real antes de programar
/// #254. Usa las zapatas seleccionadas o, sin selección, todas. Las pruebas se deshacen: el modelo queda como estaba.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class ComandoDiagnosticoZapata : IExternalCommand
{
    private static readonly string Ruta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CQEING", "zapatas", "diagnostico.txt");

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument ui = commandData.Application.ActiveUIDocument;
        Document doc = ui.Document;
        var s = new StringBuilder();
        s.AppendLine($"Modelo: {doc.Title} | Revit {commandData.Application.Application.VersionNumber}.{commandData.Application.Application.SubVersionNumber} | {DateTime.Now:s}");
        s.AppendLine($"Compartido: {doc.IsWorkshared} | Solo lectura: {doc.IsReadOnly} | Vista: {doc.ActiveView.Name} ({doc.ActiveView.ViewType})");

        List<Element> zapatas = ui.Selection.GetElementIds().Select(doc.GetElement)
            .Where(e => e?.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralFoundation).ToList();
        bool seleccion = zapatas.Count > 0;
        if (!seleccion)
            zapatas = LectorZapatas.Elementos(doc).ToList();
        s.AppendLine($"Cimentaciones: {zapatas.Count} ({(seleccion ? "seleccionadas" : "todas")})");
        Catalogo(doc, s);

        List<Grid> ejes = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
        var leidas = new List<(Element Elemento, ZapataLeida Zapata)>();
        foreach (Element e in zapatas.Take(60))
        {
            s.AppendLine();
            s.AppendLine($"== {e.Id} | {e.GetType().Name} | {e.Category?.Name} | {e.Name}");
            try
            {
                bool host = RebarHostData.IsValidHost(e);
                s.AppendLine($"  Puede llevar acero: {host} | Armadura existente: {RebarHostData.GetRebarHostData(e)?.GetRebarsInHost().Count ?? 0} | Sólidos: {LectorZapatas.SolidoMayor(e) is not null}");
                Parametros(doc, e, s);
                if (LectorZapatas.Leer(doc, e, ejes) is not { } z)
                {
                    s.AppendLine("  No se pudo leer: sin sólido o sin cara inferior plana.");
                    continue;
                }
                (double ancho, double alto) = Geometria.Caja(z.Contorno);
                s.AppendLine($"  {z.Nombre} | eje '{z.Eje}' | nivel '{z.Nivel}' | {z.Tipo} {z.Forma} | {ancho:0} x {alto:0} | h {z.Altura:0} | ángulo {z.Angulo * 180 / Math.PI:0.##}° | base {z.ZBase:0}");
                s.AppendLine($"  Contorno: {string.Join(" ", z.Contorno.Select(p => $"({p.X:0},{p.Y:0})"))}");
                s.AppendLine($"  Columnas: {string.Join(" ", z.Columnas.Select(c => $"{c.Id}@({c.Centro.X:0},{c.Centro.Y:0}) {c.Ancho:0}x{c.Alto:0}"))}");
                if (host)
                    leidas.Add((e, z));
            }
            catch (Exception ex)
            {
                s.AppendLine($"  FALLÓ LA LECTURA: {ex.Message}");
            }
        }

        // Una prueba por cada forma distinta, para no alargarlo.
        foreach ((Element e, ZapataLeida z) in leidas.GroupBy(x => (x.Zapata.Tipo, x.Zapata.Forma)).Select(g => g.First()).Take(4))
            Pruebas(doc, e, z, s);

        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, s.ToString());
        TaskDialog.Show("Diagnóstico de zapatas", $"Listo. {zapatas.Count} cimentaciones revisadas; el modelo quedó como estaba.\n\n{Ruta}");
        return Result.Succeeded;
    }

    private static void Catalogo(Document doc, StringBuilder s)
    {
        s.AppendLine();
        s.AppendLine("-- Tipos de barra");
        foreach (RebarBarType b in new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().OrderBy(b => b.BarNominalDiameter))
            s.AppendLine($"  {b.Name} | d {LectorZapatas.Mm(b.BarNominalDiameter):0.#} | doblado {LectorZapatas.Mm(b.StandardBendDiameter):0.#} | doblado gancho {LectorZapatas.Mm(b.StandardHookBendDiameter):0.#}");
        s.AppendLine("-- Tipos de gancho");
        foreach (RebarHookType h in new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>().OrderBy(h => h.HookAngle))
            s.AppendLine($"  {h.Name} | {h.HookAngle * 180 / Math.PI:0}° | {h.Style} | extensión {h.StraightLineMultiplier:0.##} d");
        s.AppendLine("-- Tipos de recubrimiento");
        foreach (RebarCoverType c in new FilteredElementCollector(doc).OfClass(typeof(RebarCoverType)).Cast<RebarCoverType>())
            s.AppendLine($"  {c.Name} | {LectorZapatas.Mm(c.CoverDistance):0.#} mm");
        s.AppendLine("-- Formas de armadura");
        foreach (RebarShape f in new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().OrderBy(f => f.Name))
        {
            string curvas;
            try
            {
                IList<Curve> lista = f.GetCurvesForBrowser();
                curvas = $"{lista.Count} ({string.Join(" ", lista.Select(c => $"{c.GetType().Name[0]}{LectorZapatas.Mm(c.Length):0}"))})";
            }
            catch (Exception ex)
            {
                curvas = $"error {ex.GetType().Name}";
            }
            s.AppendLine($"  {f.Name} | {f.RebarStyle} | curvas {curvas}");
        }
    }

    private static void Parametros(Document doc, Element e, StringBuilder s)
    {
        foreach (BuiltInParameter cara in new[] { BuiltInParameter.CLEAR_COVER_TOP, BuiltInParameter.CLEAR_COVER_BOTTOM, BuiltInParameter.CLEAR_COVER_OTHER })
        {
            Parameter? p = e.get_Parameter(cara);
            string valor = p is null ? "no existe" : doc.GetElement(p.AsElementId()) is RebarCoverType t
                ? $"{t.Name} = {LectorZapatas.Mm(t.CoverDistance):0.#} mm" : "sin tipo";
            s.AppendLine($"  {cara}: {valor}{(p?.IsReadOnly == true ? " (solo lectura)" : "")}");
        }
    }

    private static void Pruebas(Document doc, Element host, ZapataLeida z, StringBuilder s)
    {
        s.AppendLine();
        s.AppendLine($"#### PRUEBAS en {z.Nombre} ({z.Tipo} {z.Forma}); se deshacen");
        double recInf = z.RecubrimientoInferior ?? 75, recLat = z.RecubrimientoLateral ?? 75, recSup = z.RecubrimientoSuperior ?? 75;
        RebarBarType? barra = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
            .OrderBy(b => Math.Abs(LectorZapatas.Mm(b.BarNominalDiameter) - 15.9)).FirstOrDefault();
        if (barra is null)
        {
            s.AppendLine("  El modelo no tiene tipos de barra.");
            return;
        }
        double d = LectorZapatas.Mm(barra.BarNominalDiameter);
        List<Punto> dentro = Geometria.HaciaAdentro(z.Contorno, recLat);
        if (dentro.Count == 0)
        {
            s.AppendLine("  No da para el recubrimiento.");
            return;
        }
        double yMedio = (dentro.Min(p => p.Y) + dentro.Max(p => p.Y)) / 2;
        (double desde, double hasta) = Geometria.Cortes(dentro, yMedio).First();
        double zBarra = recInf + d / 2;
        double pata = z.Altura - recInf - recSup;
        XYZ P(double x, double y, double h)
        {
            Punto m = Geometria.Girar([new Punto(x, y)], z.Angulo)[0];
            return new XYZ(LectorZapatas.Pies(m.X), LectorZapatas.Pies(m.Y), LectorZapatas.Pies(z.ZBase + h));
        }
        XYZ normal = (P(0, 1, 0) - P(0, 0, 0)).Normalize();
        s.AppendLine($"  Barra {barra.Name}, d {d:0.#}. Plan: x {desde:0} a {hasta:0}, y {yMedio:0}, z {zBarra:0.#}, pata {pata:0}. Recubrimientos abajo {recInf:0}, lados {recLat:0}, arriba {recSup:0}.");

        using var t = new Transaction(doc, "Zapatas: diagnóstico");
        FailureHandlingOptions opciones = t.GetFailureHandlingOptions();
        var fallas = new SinDialogos(s);
        opciones.SetFailuresPreprocessor(fallas);
        t.SetFailureHandlingOptions(opciones);
        try
        {
            t.Start();

            // A: las patas son parte de las curvas, como una forma en U.
            double xi = desde + d / 2, xf = hasta - d / 2, tope = zBarra + pata - d / 2;
            List<Curve> enU = [Line.CreateBound(P(xi, yMedio, tope), P(xi, yMedio, zBarra)), Line.CreateBound(P(xi, yMedio, zBarra), P(xf, yMedio, zBarra)), Line.CreateBound(P(xf, yMedio, zBarra), P(xf, yMedio, tope))];
            Probar("A1 U con forma existente", () => CrearCurvas(doc, barra, host, normal, enU, null, null, nueva: false), xi, xf, zBarra, tope);
            Probar("A2 U creando forma", () => CrearCurvas(doc, barra, host, normal, enU, null, null, nueva: true), xi, xf, zBarra, tope);

            // B: una línea recta con ganchos propios de 90°, como vigas.
            double radio = LectorZapatas.Mm(barra.StandardHookBendDiameter) / 2 + d / 2;
            RebarHookType gancho = RebarHookType.Create(doc, Math.PI / 2, Math.Max(1, (pata - radio - d / 2) / d));
            gancho.Name = "CQEING Diagnóstico 90";
            gancho.Style = RebarStyle.Standard;
            List<Curve> recta = [Line.CreateBound(P(desde, yMedio, zBarra), P(hasta, yMedio, zBarra))];
            Rebar? conGanchos = Probar("B recta con gancho propio", () => CrearCurvas(doc, barra, host, normal, recta, gancho, gancho, nueva: true), xi, xf, zBarra, tope);

            // C: repartir la barra como conjunto.
            if (conGanchos is not null)
            {
                double largoSet = dentro.Max(p => p.Y) - dentro.Min(p => p.Y) - d;
                try
                {
                    conGanchos.GetShapeDrivenAccessor().SetLayoutAsFixedNumber(5, LectorZapatas.Pies(largoSet / 2), true, true, true);
                    doc.Regenerate();
                    s.AppendLine($"  C conjunto: {conGanchos.NumberOfBarPositions} barras en {largoSet / 2:0} mm, largo total {LectorZapatas.Mm(conGanchos.TotalLength):0} mm");
                }
                catch (Exception ex)
                {
                    s.AppendLine($"  C conjunto FALLÓ: {ex.Message}");
                }
            }

            // D: escribir un recubrimiento propio en la zapata.
            try
            {
                RebarCoverType cover = RebarCoverType.Create(doc, "CQEING Diagnóstico 60", LectorZapatas.Pies(60));
                foreach (BuiltInParameter cara in new[] { BuiltInParameter.CLEAR_COVER_TOP, BuiltInParameter.CLEAR_COVER_BOTTOM, BuiltInParameter.CLEAR_COVER_OTHER })
                    s.AppendLine($"  D {cara}: escrito = {host.get_Parameter(cara)?.Set(cover.Id).ToString() ?? "no existe"}");
                doc.Regenerate();
            }
            catch (Exception ex)
            {
                s.AppendLine($"  D recubrimiento FALLÓ: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            s.AppendLine($"  LA PRUEBA FALLÓ: {ex}");
        }
        finally
        {
            if (t.HasStarted() && !t.HasEnded())
                t.RollBack();
        }

        Rebar? Probar(string nombre, Func<Rebar?> crear, double xi, double xf, double zb, double tope)
        {
            try
            {
                Rebar? r = crear();
                if (r is null)
                {
                    s.AppendLine($"  {nombre}: Revit devolvió null");
                    return null;
                }
                doc.Regenerate();
                string forma = doc.GetElement(r.GetShapeId())?.Name ?? "?";
                IList<Curve> curvas = r.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
                var puntos = curvas.SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) }).ToList();
                XYZ ejeX = (P(1, 0, 0) - P(0, 0, 0)).Normalize();
                XYZ o = P(0, 0, 0);
                double X(XYZ p) => LectorZapatas.Mm((p - o).DotProduct(ejeX));
                double H(XYZ p) => LectorZapatas.Mm(p.Z) - z.ZBase;
                double largo = LectorZapatas.Mm(r.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH)?.AsDouble() ?? r.TotalLength);
                s.AppendLine($"  {nombre}: forma '{forma}' | x {puntos.Min(X):0.#} a {puntos.Max(X):0.#} (plan {xi:0.#} a {xf:0.#}) | h {puntos.Min(H):0.#} a {puntos.Max(H):0.#} (plan {zb:0.#} a {tope:0.#}) | largo {largo:0.#}");
                s.AppendLine($"    curvas: {string.Join(" ", curvas.Select(c => $"{c.GetType().Name[0]}{LectorZapatas.Mm(c.Length):0.#}"))}");
                return r;
            }
            catch (Exception ex)
            {
                s.AppendLine($"  {nombre} FALLÓ: {ex.Message}");
                return null;
            }
        }
    }

    private static Rebar? CrearCurvas(Document doc, RebarBarType barra, Element host, XYZ normal, IList<Curve> curvas, RebarHookType? inicio, RebarHookType? fin, bool nueva)
    {
        using var terminaciones = new BarTerminationsData(doc);
        if (inicio is not null)
        {
            terminaciones.HookTypeIdAtStart = inicio.Id;
            terminaciones.TerminationOrientationAtStart = RebarTerminationOrientation.Right;
        }
        if (fin is not null)
        {
            terminaciones.HookTypeIdAtEnd = fin.Id;
            terminaciones.TerminationOrientationAtEnd = RebarTerminationOrientation.Right;
        }
        return Rebar.CreateFromCurves(doc, RebarStyle.Standard, barra, host, normal, curvas, terminaciones, useExistingShapeIfPossible: true, createNewShape: nueva);
    }

    /// <summary>Anota lo que dice Revit en vez de mostrarle diálogos al usuario.</summary>
    private sealed class SinDialogos(StringBuilder s) : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            foreach (FailureMessageAccessor m in a.GetFailureMessages())
            {
                s.AppendLine($"    Revit {m.GetSeverity()}: {m.GetDescriptionText()}");
                if (m.GetSeverity() == FailureSeverity.Warning)
                    a.DeleteWarning(m);
            }
            return FailureProcessingResult.Continue;
        }
    }
}
#endif
