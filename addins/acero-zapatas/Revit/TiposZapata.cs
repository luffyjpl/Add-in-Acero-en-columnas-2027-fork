using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// Los tipos del proyecto para el tablero y la generación (#250). Solo se leen: nunca se edita un tipo que ya tiene el
/// proyecto. De las formas se muestran las curvas de <c>GetCurvesForBrowser</c>, las mismas del navegador de Revit.
/// </summary>
internal static class TiposZapata
{
    /// <summary>Tolerancia con el diámetro de la regla, en mm: «#2» de 50 mm en el modelo de prueba no pasa por un #2.</summary>
    private const double ToleranciaDiametro = 0.5;

    /// <summary>El tipo de barra de cada número: primero el de cimentación, luego el que se llama <c>#n</c> (decisión 24).</summary>
    public static Dictionary<int, RebarBarType> Barras(Document doc, Reglas reglas)
    {
        List<RebarBarType> tipos = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
        var barras = new Dictionary<int, RebarBarType>();
        foreach ((int numero, ReglaDiametro regla) in reglas.PorDiametro)
        {
            string nombre = $"#{numero}";
            RebarBarType? elegido = tipos.Where(t => Math.Abs(LectorZapatas.Mm(t.BarNominalDiameter) - regla.Diametro) <= ToleranciaDiametro)
                .OrderBy(t => t.Name.Trim().Equals($"{nombre} CIM", StringComparison.OrdinalIgnoreCase) ? 0
                    : t.Name.Contains("CIM", StringComparison.OrdinalIgnoreCase) && t.Name.Contains(nombre, StringComparison.Ordinal) ? 1
                    : t.Name.Trim() == nombre ? 2 : 3)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (elegido is not null)
                barras[numero] = elegido;
        }
        return barras;
    }

    public static CatalogoZapatas Catalogo(Document doc, Reglas reglas)
    {
        List<RebarHookType> tiposGancho = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>()
            .Where(h => h.Style == RebarStyle.Standard).OrderBy(h => h.HookAngle).ThenBy(h => h.Name).ToList();
        List<GanchoCatalogo> ganchos = tiposGancho
            .Select(h => new GanchoCatalogo(h.Id.ToString(), h.Name, (int)Math.Round(h.HookAngle * 180 / Math.PI), h.StraightLineMultiplier)).ToList();

        RebarHookType? estribo = GanchoEstribo(doc);
        List<BarraCatalogo> barras = Barras(doc, reglas).OrderBy(b => b.Key)
            .Select(b => new BarraCatalogo(b.Key, b.Value.Name, LectorZapatas.Mm(b.Value.BarNominalDiameter), LectorZapatas.Mm(b.Value.StandardHookBendDiameter),
                reglas.PorDiametro[b.Key].Gancho90.B, reglas.PorDiametro[b.Key].Estribo135.B, reglas.PorDiametro[b.Key].Gancho180.B, Colas(b.Value, tiposGancho),
                LectorZapatas.Mm(b.Value.StirrupTieBendDiameter), ColaEstribo(b.Value, estribo))).ToList();

        var formas = new List<FormaCatalogo>();
        foreach (RebarShape f in new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().Where(f => f.RebarStyle == RebarStyle.Standard).OrderBy(f => f.Name))
        {
            IList<Curve> curvas;
            try
            {
                curvas = f.GetCurvesForBrowser();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                continue;
            }
            if (Clase(curvas) is ClaseForma clase)
                formas.Add(new FormaCatalogo(f.Id.ToString(), f.Name, Dibujo(curvas), clase));
        }
        if (Cerrada(doc) is { } cerrada)
            formas.Add(cerrada);
        return new CatalogoZapatas(barras, formas, ganchos);
    }

    /// <summary>
    /// La forma de la barra cerrada (decisión 43 del plan de 2026-11-05): de estilo estribo, un rectángulo de 4 tramos con
    /// 135° en las dos puntas, como la T1 (H21). El add-in de columnas deja muchas iguales («Forma de armadura N»); con una
    /// alcanza, porque Revit da la misma barra con cualquiera. Se prefiere la de nombre más corto (T1).
    /// </summary>
    private static FormaCatalogo? Cerrada(Document doc)
    {
        foreach (RebarShape f in new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>()
            .Where(f => f.RebarStyle == RebarStyle.StirrupTie).OrderBy(f => f.Name.Length).ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            // GetDefaultHookAngle está obsoleto desde 2026 y no existe en 2027; Revit indica usar GetTerminationsData.
            var extremos = f.GetTerminationsData();
            if (Math.Abs(extremos.HookAngleAtStart - 135) > 0.5 || Math.Abs(extremos.HookAngleAtEnd - 135) > 0.5)
                continue;
            IList<Curve> curvas;
            try
            {
                curvas = f.GetCurvesForBrowser();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                continue;
            }
            if (curvas.Count != 4 || !curvas.All(c => c is Line))
                continue;
            List<XYZ> d = curvas.Cast<Line>().Select(l => l.Direction).ToList();
            bool rectangulo = Enumerable.Range(0, 3).All(i => Math.Abs(d[i].DotProduct(d[i + 1])) < 1e-3)
                && d[0].DotProduct(d[2]) < -0.999 && d[1].DotProduct(d[3]) < -0.999;
            if (rectangulo)
                return new FormaCatalogo(f.Id.ToString(), f.Name, Dibujo(curvas), ClaseForma.Cerrada);
        }
        return null;
    }

    /// <summary>
    /// El gancho de 135° de estribo para la barra cerrada (H21): de estilo estribo, 135°, el de cola más corta (el normal
    /// antes que el sísmico). Null si el proyecto no tiene ninguno.
    /// </summary>
    public static RebarHookType? GanchoEstribo(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>()
            .Where(h => h.Style == RebarStyle.StirrupTie && Math.Abs(h.HookAngle * 180 / Math.PI - 135) < 0.5)
            .OrderBy(h => h.StraightLineMultiplier).ThenBy(h => h.Name, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>La cola recta del gancho de estribo en esta barra, como <see cref="Colas"/> pero con el doblez de estribo.</summary>
    private static double? ColaEstribo(RebarBarType barra, RebarHookType? gancho)
    {
        if (gancho is null)
            return null;
        double cola = LectorZapatas.Mm(barra.GetHookTangentLength(gancho.Id)) - LectorZapatas.Mm(barra.StirrupTieBendDiameter) / 2 - LectorZapatas.Mm(barra.BarNominalDiameter);
        return cola > 0 ? Math.Round(cola, 2) : null;
    }

    /// <summary>
    /// La cola recta que deja Revit con cada gancho de 135° y 180° en esta barra: <c>GetHookTangentLength − D/2 − d</c>
    /// (H13 del plan de 2026-11-05, medido en 12 casos). Un gancho sin largo para esta barra no va: la vista previa usa el
    /// del cuadro.
    /// </summary>
    private static Dictionary<string, double> Colas(RebarBarType barra, IEnumerable<RebarHookType> ganchos)
    {
        double d = LectorZapatas.Mm(barra.BarNominalDiameter), doblado = LectorZapatas.Mm(barra.StandardHookBendDiameter);
        var colas = new Dictionary<string, double>();
        foreach (RebarHookType h in ganchos.Where(h => Math.Round(h.HookAngle * 180 / Math.PI) is 135 or 180))
        {
            double cola = LectorZapatas.Mm(barra.GetHookTangentLength(h.Id)) - doblado / 2 - d;
            if (cola > 0)
                colas[h.Id.ToString()] = Math.Round(cola, 2);
        }
        return colas;
    }

    /// <summary>
    /// Recta, L o U, por la geometría de la forma (decisión 37 y H16): recta, 1 tramo; L, 2 tramos a 90°; U, 3 tramos a
    /// 90° con las dos patas del mismo lado. Las demás, null: Revit no las acepta para una parrilla (H8) y no se muestran.
    /// </summary>
    private static ClaseForma? Clase(IList<Curve> curvas)
    {
        if (!curvas.All(c => c is Line))
            return null;
        List<XYZ> d = curvas.Cast<Line>().Select(l => l.Direction).ToList();
        bool Escuadra(int i) => Math.Abs(d[i].DotProduct(d[i + 1])) < 1e-3;
        return curvas.Count switch
        {
            1 => ClaseForma.Recta,
            2 when Escuadra(0) => ClaseForma.L,
            3 when Escuadra(0) && Escuadra(1) && d[0].DotProduct(d[2]) < -0.999 => ClaseForma.U,
            _ => null,
        };
    }

    /// <summary>Las curvas en su plano, en mm: la forma puede venir en cualquier plano del espacio.</summary>
    private static List<IReadOnlyList<double[]>> Dibujo(IList<Curve> curvas)
    {
        List<List<XYZ>> trazos = curvas.Select(c => c.Tessellate().ToList()).ToList();
        List<XYZ> puntos = trazos.SelectMany(t => t).ToList();
        if (puntos.Count == 0)
            return [];
        XYZ origen = puntos[0];
        XYZ? u = puntos.Select(p => p - origen).Where(v => v.GetLength() > 1e-6).Select(v => v.Normalize()).FirstOrDefault();
        if (u is null)
            return [];
        XYZ? normal = puntos.Select(p => u.CrossProduct(p - origen)).Where(n => n.GetLength() > 1e-6).Select(n => n.Normalize()).FirstOrDefault();
        XYZ v = (normal ?? (Math.Abs(u.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX).CrossProduct(u).Normalize()).CrossProduct(u).Normalize();
        return trazos.Select(t => (IReadOnlyList<double[]>)t.Select(p => new[] { Math.Round(LectorZapatas.Mm((p - origen).DotProduct(u)), 1), Math.Round(LectorZapatas.Mm((p - origen).DotProduct(v)), 1) }).ToList()).ToList();
    }

    /// <summary>
    /// El tipo de recubrimiento para esa distancia: uno del proyecto que ya mida eso o uno propio
    /// (<c>CQEING Recubrimiento 60</c>). Dentro de una transacción.
    /// </summary>
    public static RebarCoverType Recubrimiento(Document doc, double mm)
    {
        List<RebarCoverType> tipos = new FilteredElementCollector(doc).OfClass(typeof(RebarCoverType)).Cast<RebarCoverType>().ToList();
        return tipos.FirstOrDefault(t => Math.Abs(LectorZapatas.Mm(t.CoverDistance) - mm) < 0.5)
            ?? RebarCoverType.Create(doc, $"CQEING Recubrimiento {mm:0.#}", LectorZapatas.Pies(mm));
    }
}
