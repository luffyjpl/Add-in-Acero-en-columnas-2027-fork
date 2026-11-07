using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// Lee las zapatas del modelo (#248): contorno y altura salen de la geometría del sólido, no de parámetros del tipo,
/// porque una combinada puede no ser un rectángulo. No cambia nada del modelo.
/// </summary>
internal static class LectorZapatas
{
    /// <summary>Una columna cuenta si arranca hasta 500 mm por encima de la cara superior de la zapata.</summary>
    private const double HolguraColumnaMm = 500;

    public static double Mm(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Millimeters);

    public static double Pies(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

    public static IEnumerable<Element> Elementos(Document doc) => new FilteredElementCollector(doc)
        .OfCategory(BuiltInCategory.OST_StructuralFoundation).WhereElementIsNotElementType().ToElements();

    /// <summary>Las zapatas del modelo que pueden llevar acero y tienen un sólido legible.</summary>
    public static List<ZapataLeida> Leer(Document doc)
    {
        List<Grid> ejes = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
        var zapatas = new List<ZapataLeida>();
        foreach (Element e in Elementos(doc))
            if (RebarHostData.IsValidHost(e) && Leer(doc, e, ejes) is { } z)
                zapatas.Add(z);
        // Sin marca en el modelo se numeran por eje, para la lista del tablero («Z-1 · Eje 1-A»).
        int numero = 0;
        return zapatas.OrderBy(z => z.Eje, StringComparer.CurrentCultureIgnoreCase).ThenBy(z => z.Id)
            .Select(z => { numero++; return string.IsNullOrEmpty(z.Nombre) ? z with { Nombre = $"Z-{numero}" } : z; }).ToList();
    }

    public static ZapataLeida? Leer(Document doc, Element e, IReadOnlyList<Grid> ejes)
    {
        if (SolidoMayor(e) is not { } solido || CaraInferior(solido) is not { } cara || Contorno(cara) is not { Count: >= 3 } enModelo)
            return null;

        List<XYZ> esquinas = Vertices(solido);
        double zBase = Mm(esquinas.Min(p => p.Z)), zTope = Mm(esquinas.Max(p => p.Z));

        List<Element> columnas = ColumnasEncima(doc, enModelo, zTope);
        double angulo = Geometria.AnguloEjes(enModelo, columnas.OfType<FamilyInstance>()
            .Select(c => Math.Atan2(c.HandOrientation.Y, c.HandOrientation.X)).ToList());
        List<Punto> contorno = Geometria.Girar(enModelo, -angulo);
        return new ZapataLeida(e.Id.ToString(), Nombre(e), Eje(ejes, enModelo), Nivel(doc, e), angulo, zBase, zTope - zBase, contorno,
            columnas.Select(c => Columna(c, angulo)).ToList(), Recubrimiento(doc, e, BuiltInParameter.CLEAR_COVER_BOTTOM),
            Recubrimiento(doc, e, BuiltInParameter.CLEAR_COVER_OTHER), Recubrimiento(doc, e, BuiltInParameter.CLEAR_COVER_TOP));
    }

    /// <summary>La marca del elemento; vacío si no tiene.</summary>
    private static string Nombre(Element e) => e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()?.Trim() ?? string.Empty;

    private static string Nivel(Document doc, Element e) => doc.GetElement(e.LevelId) is Level nivel ? nivel.Name : string.Empty;

    /// <summary>Los dos ejes no paralelos más cercanos al centro: <c>A-1</c>. Vacío si están a más de 2 m.</summary>
    private static string Eje(IReadOnlyList<Grid> ejes, List<Punto> contorno)
    {
        if (ejes.Count == 0)
            return string.Empty;
        var centro = new XYZ(LectorZapatas.Pies(contorno.Average(p => p.X)), LectorZapatas.Pies(contorno.Average(p => p.Y)), 0);
        var cerca = ejes.Select(g => (Eje: g, Linea: (Line)g.Curve)).Select(x =>
        {
            Line plana = Line.CreateUnbound(new XYZ(x.Linea.Origin.X, x.Linea.Origin.Y, 0), new XYZ(x.Linea.Direction.X, x.Linea.Direction.Y, 0));
            return (x.Eje.Name, plana.Direction, Distancia: plana.Distance(centro));
        }).Where(x => Mm(x.Distancia) <= 2000).OrderBy(x => x.Distancia).ToList();
        if (cerca.Count == 0)
            return string.Empty;
        var primero = cerca[0];
        var segundo = cerca.Skip(1).FirstOrDefault(x => Math.Abs(x.Direction.CrossProduct(primero.Direction).Z) > 0.1);
        return segundo.Name is null ? primero.Name : string.Join("-", new[] { primero.Name, segundo.Name }.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
    }

    /// <summary>Las columnas que arrancan sobre la zapata, con el centro dentro del contorno (en el modelo).</summary>
    private static List<Element> ColumnasEncima(Document doc, List<Punto> contorno, double zTope)
    {
        double xMin = Pies(contorno.Min(p => p.X)), xMax = Pies(contorno.Max(p => p.X));
        double yMin = Pies(contorno.Min(p => p.Y)), yMax = Pies(contorno.Max(p => p.Y));
        var filtro = new BoundingBoxIntersectsFilter(new Outline(new XYZ(xMin, yMin, Pies(zTope - HolguraColumnaMm)), new XYZ(xMax, yMax, Pies(zTope + HolguraColumnaMm))));
        return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType().WherePasses(filtro)
            .Where(c => c.get_BoundingBox(null) is { } caja && Mm(caja.Min.Z) <= zTope + HolguraColumnaMm
                        && Dentro(contorno, new Punto(Mm((caja.Min.X + caja.Max.X) / 2), Mm((caja.Min.Y + caja.Max.Y) / 2))))
            .OrderBy(c => c.Id.Value).ToList();
    }

    /// <summary>
    /// La columna en los ejes de la zapata. La huella sale de los vértices de su sólido girados a esos ejes, no de la
    /// caja del modelo, que crece si la columna está girada.
    /// </summary>
    private static ColumnaLeida Columna(Element c, double angulo)
    {
        BoundingBoxXYZ caja = c.get_BoundingBox(null);
        List<Punto> puntos = SolidoMayor(c) is { } solido
            ? Vertices(solido).Select(p => new Punto(Mm(p.X), Mm(p.Y))).ToList()
            : [new(Mm(caja.Min.X), Mm(caja.Min.Y)), new(Mm(caja.Max.X), Mm(caja.Max.Y))];
        List<Punto> huella = Geometria.Girar(puntos, -angulo);
        (double ancho, double alto) = Geometria.Caja(huella);
        var centro = new Punto((huella.Min(p => p.X) + huella.Max(p => p.X)) / 2, (huella.Min(p => p.Y) + huella.Max(p => p.Y)) / 2);
        return new ColumnaLeida(c.Id.ToString(), centro, ancho, alto);
    }

    private static bool Dentro(List<Punto> contorno, Punto p) => Geometria.Cortes(contorno, p.Y).Any(t => p.X >= t.Desde && p.X <= t.Hasta);

    /// <summary>El recubrimiento de esa cara en mm; null si el elemento no lo trae.</summary>
    private static double? Recubrimiento(Document doc, Element e, BuiltInParameter cara) =>
        e.get_Parameter(cara) is { } p && doc.GetElement(p.AsElementId()) is RebarCoverType tipo ? Mm(tipo.CoverDistance) : null;

    public static Solid? SolidoMayor(Element e) =>
        Solidos(e.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).OrderByDescending(s => s.Volume).FirstOrDefault();

    private static IEnumerable<Solid> Solidos(GeometryElement? geometria)
    {
        if (geometria is null)
            yield break;
        foreach (GeometryObject objeto in geometria)
        {
            if (objeto is Solid { Volume: > 0 } solido)
                yield return solido;
            else if (objeto is GeometryInstance instancia)
                foreach (Solid s in Solidos(instancia.GetInstanceGeometry()))
                    yield return s;
        }
    }

    private static List<XYZ> Vertices(Solid solido) =>
        solido.Edges.Cast<Edge>().SelectMany(a => a.Tessellate()).ToList();

    /// <summary>La cara plana horizontal más baja del sólido, mirando hacia abajo.</summary>
    private static PlanarFace? CaraInferior(Solid solido) => solido.Faces.OfType<PlanarFace>()
        .Where(c => c.FaceNormal.Z < -0.99).OrderBy(c => c.Origin.Z).ThenByDescending(c => c.Area).FirstOrDefault();

    /// <summary>El lazo exterior de la cara, en mm y en planta, sin puntos repetidos ni alineados.</summary>
    private static List<Punto>? Contorno(PlanarFace cara)
    {
        List<Punto>? mayor = null;
        double area = 0;
        foreach (EdgeArray lazo in cara.EdgeLoops)
        {
            var puntos = new List<Punto>();
            foreach (Edge arista in lazo)
            {
                IList<XYZ> tramo = arista.AsCurveFollowingFace(cara).Tessellate();
                foreach (XYZ p in tramo.Take(tramo.Count - 1))
                    puntos.Add(new Punto(Mm(p.X), Mm(p.Y)));
            }
            double a = Math.Abs(Geometria.Area(puntos));
            if (a > area)
                (mayor, area) = (puntos, a);
        }
        return mayor is null ? null : SinAlineados(mayor);
    }

    private static List<Punto> SinAlineados(List<Punto> c)
    {
        var r = new List<Punto>();
        for (int i = 0; i < c.Count; i++)
        {
            Punto a = c[(i + c.Count - 1) % c.Count], b = c[i], d = c[(i + 1) % c.Count];
            double cruz = (b.X - a.X) * (d.Y - b.Y) - (b.Y - a.Y) * (d.X - b.X);
            double largos = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * Math.Sqrt((d.X - b.X) * (d.X - b.X) + (d.Y - b.Y) * (d.Y - b.Y));
            if (largos > 1e-6 && Math.Abs(cruz) > largos * 1e-4)
                r.Add(b);
        }
        return r;
    }
}
