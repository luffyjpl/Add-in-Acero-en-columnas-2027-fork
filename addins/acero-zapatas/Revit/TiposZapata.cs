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
        List<BarraCatalogo> barras = Barras(doc, reglas).OrderBy(b => b.Key)
            .Select(b => new BarraCatalogo(b.Key, b.Value.Name, LectorZapatas.Mm(b.Value.BarNominalDiameter), LectorZapatas.Mm(b.Value.StandardHookBendDiameter),
                reglas.PorDiametro[b.Key].Gancho90.B, reglas.PorDiametro[b.Key].Estribo135.B, reglas.PorDiametro[b.Key].Gancho180.B)).ToList();

        List<GanchoCatalogo> ganchos = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>()
            .Where(h => h.Style == RebarStyle.Standard).OrderBy(h => h.HookAngle).ThenBy(h => h.Name)
            .Select(h => new GanchoCatalogo(h.Id.ToString(), h.Name, (int)Math.Round(h.HookAngle * 180 / Math.PI), h.StraightLineMultiplier)).ToList();

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
            List<IReadOnlyList<double[]>> dibujo = Dibujo(curvas);
            formas.Add(new FormaCatalogo(f.Id.ToString(), f.Name, dibujo, EsDeParrilla(curvas)));
        }
        return new CatalogoZapatas(barras, formas, ganchos);
    }

    /// <summary>Recta o en U: hasta tres tramos rectos. El resto se muestra atenuado, bajo el filtro «Todas».</summary>
    private static bool EsDeParrilla(IList<Curve> curvas) => curvas.Count is 1 or 3 && curvas.All(c => c is Line);

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
