using Autodesk.Revit.DB;
using CQEING.Vigas.Calculo.Porticos;
using CQEING.Viguetas.Calculo;

namespace CQEING.Viguetas.Revit;

/// <summary>
/// Adaptador de Revit para «Crear viguetas» (#948): los niveles que tienen armazón estructural y, por nivel, sus
/// vigas y viguetas (eje por el centro de la geometría y ancho medido, como el lector del add-in de vigas) y sus columnas.
/// Todo en mm.
/// </summary>
internal static class LectorPiso
{
    /// <summary>Lo que escribe el add-in en Comentarios de cada vigueta que crea (decisión 7 del plan).</summary>
    public const string Marca = "CQEING viguetas";

    /// <summary>Una columna cuenta en el piso si llega a menos de esto (mm) de la cota del nivel.</summary>
    private const double ToleranciaColumna = 100;

    public static List<PisoResumen> Pisos(Document doc) =>
        Armazon(doc).Select(f => (Elemento: f, Nivel: NivelDe(f))).Where(x => x.Nivel is not null)
            .GroupBy(x => x.Nivel!.Id)
            .Select(g => new PisoResumen(g.Key.ToString(), g.First().Nivel!.Name, Medidas.Mm(g.First().Nivel!.Elevation), g.Count(), g.Count(x => EsCreada(x.Elemento))))
            .OrderBy(p => p.Cota)
            .ToList();

    public static PisoLeido Leer(Document doc, Level nivel)
    {
        var armazon = new List<ArmazonPiso>();
        foreach (FamilyInstance f in Armazon(doc).Where(f => NivelDe(f)?.Id == nivel.Id))
            if (Leer(f) is ArmazonPiso a)
                armazon.Add(a);

        double cota = Medidas.Mm(nivel.Elevation);
        var columnas = new List<ColumnaPiso>();
        foreach (FamilyInstance c in Instancias(doc, BuiltInCategory.OST_StructuralColumns))
        {
            List<XYZ> puntos = Medidas.Puntos(c);
            if (puntos.Count == 0)
                continue;
            double abajo = Medidas.Mm(puntos.Min(p => p.Z)), arriba = Medidas.Mm(puntos.Max(p => p.Z));
            if (abajo - ToleranciaColumna <= cota && cota <= arriba + ToleranciaColumna)
                columnas.Add(new ColumnaPiso(c.Id.ToString(),
                    new Punto(Medidas.Mm(puntos.Min(p => p.X)), Medidas.Mm(puntos.Min(p => p.Y))),
                    new Punto(Medidas.Mm(puntos.Max(p => p.X)), Medidas.Mm(puntos.Max(p => p.Y)))));
        }
        return new PisoLeido(nivel.Id.ToString(), nivel.Name, cota, armazon, columnas, Suelos(doc, nivel));
    }

    /// <summary>Lados de menos de esto (mm) no dejan vértice: son restos del dibujo del boceto.</summary>
    private const double MismoVertice = 1;

    /// <summary>
    /// Los suelos del nivel (#954, decisión 3) con los contornos de su boceto (<see cref="Floor.SketchId"/> →
    /// <see cref="Sketch.Profile"/>, API pública desde Revit 2022), en planta y en mm. El de afuera va primero (el de
    /// más área); los demás son sus huecos.
    /// </summary>
    private static List<SueloPiso> Suelos(Document doc, Level nivel)
    {
        var suelos = new List<SueloPiso>();
        foreach (Floor f in new FilteredElementCollector(doc).OfClass(typeof(Floor)).WhereElementIsNotElementType().OfType<Floor>()
                     .Where(f => f.LevelId == nivel.Id))
        {
            if (doc.GetElement(f.SketchId) is not Sketch boceto)
                continue;
            List<IReadOnlyList<Punto>> contornos = boceto.Profile.Cast<CurveArray>().Select(Contorno)
                .Where(c => c.Count >= 3).OrderByDescending(c => Math.Abs(Geometria.Area(c))).Cast<IReadOnlyList<Punto>>().ToList();
            if (contornos.Count > 0)
                suelos.Add(new SueloPiso(f.Id.ToString(), contornos));
        }
        return suelos;
    }

    /// <summary>
    /// Los vértices de un lazo del boceto, en orden: cada curva se encadena con la anterior (dándola vuelta si hace falta)
    /// y los arcos se reparten en tramos rectos.
    /// </summary>
    private static List<Punto> Contorno(CurveArray lazo)
    {
        var puntos = new List<Punto>();
        foreach (Curve curva in lazo)
        {
            List<Punto> tramo = curva.Tessellate().Select(Planta).ToList();
            if (puntos.Count > 0 && Geometria.Distancia(puntos[^1], tramo[0]) > Geometria.Distancia(puntos[^1], tramo[^1]))
                tramo.Reverse();
            foreach (Punto p in tramo)
                if (puntos.Count == 0 || Geometria.Distancia(puntos[^1], p) > MismoVertice)
                    puntos.Add(p);
        }
        if (puntos.Count > 1 && Geometria.Distancia(puntos[0], puntos[^1]) <= MismoVertice)
            puntos.RemoveAt(puntos.Count - 1);
        return Geometria.SinColineales(puntos);
    }

    /// <summary>Los tipos de armazón del modelo, para elegir con cuál se crean las viguetas (decisión 8).</summary>
    public static List<TipoArmazon> Tipos(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsElementType()
            .OfType<FamilySymbol>()
            .Select(t => new TipoArmazon(t.Id.ToString(), $"{t.FamilyName} : {t.Name}", ReglaVigueta.PorNombre(t.Name)))
            .OrderByDescending(t => t.Sugerido).ThenBy(t => t.Nombre)
            .ToList();

    /// <summary>
    /// La cota (pies) de las líneas de ubicación del armazón del nivel que no creó el add-in: las viguetas nuevas van a
    /// la misma altura que las vigas que ya están.
    /// </summary>
    public static double CotaLineas(Document doc, Level nivel)
    {
        List<double> z = Armazon(doc).Where(f => NivelDe(f)?.Id == nivel.Id && !EsCreada(f))
            .Select(f => f.Location).OfType<LocationCurve>().Select(l => l.Curve.GetEndPoint(0).Z).ToList();
        return z.Count == 0 ? nivel.Elevation : z.Average();
    }

    /// <summary>Las viguetas que creó el add-in, por id.</summary>
    public static bool EsCreada(Element e) =>
        e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == Marca;

    private static ArmazonPiso? Leer(FamilyInstance f)
    {
        if (f.Location is not LocationCurve { Curve: Line linea })
            return null;
        List<XYZ> puntos = Medidas.Puntos(f);
        if (puntos.Count == 0)
            return null;
        XYZ d = linea.Direction;
        var normal = new XYZ(-d.Y, d.X, 0).Normalize();
        double ancho = puntos.Max(p => p.DotProduct(normal)) - puntos.Min(p => p.DotProduct(normal));
        // La línea de ubicación puede ir por una cara (justificación): se corre al centro de la geometría.
        var centro = new XYZ((puntos.Min(p => p.X) + puntos.Max(p => p.X)) / 2, (puntos.Min(p => p.Y) + puntos.Max(p => p.Y)) / 2, 0);
        XYZ p0 = linea.GetEndPoint(0), p1 = linea.GetEndPoint(1);
        XYZ corrimiento = normal * (centro - new XYZ(p0.X, p0.Y, 0)).DotProduct(normal);
        return new ArmazonPiso(f.Id.ToString(), Planta(p0 + corrimiento), Planta(p1 + corrimiento), Medidas.Mm(ancho),
            f.Symbol.Name, EsCreada(f));
    }

    private static IEnumerable<FamilyInstance> Armazon(Document doc) =>
        Instancias(doc, BuiltInCategory.OST_StructuralFraming).Where(f => f.Location is LocationCurve { Curve: Line });

    private static Level? NivelDe(FamilyInstance f) =>
        f.Document.GetElement(f.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId) as Level;

    private static IEnumerable<FamilyInstance> Instancias(Document doc, BuiltInCategory categoria) =>
        new FilteredElementCollector(doc).OfCategory(categoria).WhereElementIsNotElementType().OfType<FamilyInstance>();

    private static Punto Planta(XYZ p) => new(Medidas.Mm(p.X), Medidas.Mm(p.Y));
}
