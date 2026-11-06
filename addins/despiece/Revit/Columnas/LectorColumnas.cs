using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;
using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit.Columnas;

/// <summary>Una pieza de varilla: el conjunto de Revit y cuál de sus barras.</summary>
internal sealed record PiezaVarilla(ElementId Barra, int Indice, double ZInferior, double ZSuperior);

/// <summary>Una varilla vertical de la columna, de abajo hacia arriba, con su grupo de traslapo si se supo.</summary>
internal sealed record Varilla(VarillaLeida Medida, IReadOnlyList<PiezaVarilla> Piezas, GrupoTraslapo? Grupo);

/// <summary>
/// Una columna como la ve el ingeniero: la pila de elementos de Revit en la misma posición en planta (uno por piso, o uno
/// solo; hallazgos 6 y 13 del plan docs/planes/2026-10-31-despiece-columnas-226-228.md).
/// </summary>
internal sealed record ColumnaLeida(ElementoLeido Elemento, IReadOnlyList<ElementId> Elementos, XYZ Centro, double ZInferior, double ZSuperior,
    IReadOnlyList<Varilla> Varillas, FuenteGrupos? Fuente);

/// <summary>Lee las columnas del modelo para el despiece (#226). Solo lee: no escribe nada en el modelo (decisión 7).</summary>
internal static class LectorColumnas
{
    private const double PiesPorMetro = 1 / 0.3048;

    /// <summary>Lo mismo que <c>LectorContexto.ToleranciaPlanta</c> del add-in de columnas.</summary>
    private const double MismaPila = 0.05 * PiesPorMetro;

    /// <summary>Hasta aquí un eje pasa "por" la columna.</summary>
    private const double Cercania = 0.3 * PiesPorMetro;

    /// <summary>El registro de columnas generadas del add-in de columnas (<c>RegistroGeneradosRevit</c>), solo para leerlo.</summary>
    private static readonly Guid EsquemaColumnas = new("3F1A7C52-6B0E-4D2A-9E8B-71C4D5A2F0E3");

    public static List<ColumnaLeida> Leer(Document doc)
    {
        List<FamilyInstance> columnas = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType().OfType<FamilyInstance>().Where(c => c.get_BoundingBox(null) is not null).ToList();
        ILookup<ElementId, Rebar> barras = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>().ToLookup(b => b.GetHostId());
        List<Grid> ejes = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
        Dictionary<string, int> registro = VarillasDelRegistro(doc);
        var leidas = new List<ColumnaLeida>();
        foreach (List<FamilyInstance> pila in Pilas(columnas))
        {
            // Una columna que no se entiende se salta: nunca puede impedir el despiece de las vigas (decisión 7, hallazgo 24).
            try
            {
                leidas.Add(Columna(doc, pila, barras, ejes, registro));
            }
            catch (Exception e)
            {
                Registro.Paso($"columnas: no se pudo leer la columna {pila[0].Id}: {e}");
            }
        }
        return leidas;
    }

    /// <summary>Agrupa por centro en planta, como <c>AgruparPilas</c> del add-in de columnas, y ordena cada pila de abajo hacia arriba.</summary>
    private static List<List<FamilyInstance>> Pilas(List<FamilyInstance> columnas)
    {
        var pilas = new List<List<FamilyInstance>>();
        foreach (FamilyInstance c in columnas)
        {
            List<FamilyInstance>? pila = pilas.FirstOrDefault(p => CentroPlanta(p[0]).DistanceTo(CentroPlanta(c)) <= MismaPila);
            if (pila is null)
                pilas.Add(pila = []);
            pila.Add(c);
        }
        foreach (List<FamilyInstance> pila in pilas)
            pila.Sort((a, b) => a.get_BoundingBox(null).Min.Z.CompareTo(b.get_BoundingBox(null).Min.Z));
        return pilas;
    }

    private static ColumnaLeida Columna(Document doc, List<FamilyInstance> pila, ILookup<ElementId, Rebar> barras, List<Grid> ejes, Dictionary<string, int> registro)
    {
        List<BoundingBoxXYZ> cajas = pila.Select(c => c.get_BoundingBox(null)).ToList();
        double zInf = cajas.Min(c => c.Min.Z), zSup = cajas.Max(c => c.Max.Z);
        XYZ centro = CentroPlanta(pila[0]);
        // La sección es la común a toda la pila: un dado más ancho abajo no la agranda (como la ventana de columnas, 22-oct).
        int ancho = (int)Math.Round(LectorDespiece.Mm(cajas.Min(c => c.Max.X - c.Min.X)));
        int alto = (int)Math.Round(LectorDespiece.Mm(cajas.Min(c => c.Max.Y - c.Min.Y)));

        List<Rebar> propias = pila.SelectMany(c => barras[c.Id]).ToList();
        var lista = propias.Select(b =>
        {
            // Por el eje y no por la caja: hay barras de columna sin caja (hallazgo 24).
            if (b.get_BoundingBox(null) is null)
                Registro.Paso($"columnas: barra {b.Id} sin caja");
            (double abajo, double arriba) = Alturas(b);
            return new BarraLeida(doc.GetElement(b.GetTypeId())?.Name ?? "", doc.GetElement(b.GetShapeId())?.Name ?? "", b.Quantity,
                (int)Math.Round(LectorDespiece.Mm(LectorDespiece.LargoBarra(b))), (int)Math.Round(LectorDespiece.Mm((abajo + arriba) / 2 - zInf)),
                (int)Math.Round(LectorDespiece.Mm(b.GetCenterlineCurves(false, true, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0).Sum(c => c.Length))),
                EsVertical(b) ? LadoGanchos(b, centro) : "");
        }).ToList();

        List<(VarillaLeida Medida, List<PiezaVarilla> Piezas)> varillas = Varillas(propias.Where(EsVertical));
        int? delRegistro = registro.TryGetValue(pila[0].UniqueId, out int n) ? n : null;
        ResultadoGrupos? grupos = varillas.Count == 0 ? null : GruposTraslapo.Asignar(varillas.Select(v => v.Medida).ToList(), delRegistro);
        if (grupos is { Coinciden: false })
            Registro.Paso($"columna {pila[0].Id}: el registro y la altura de los traslapos no dan el mismo grupo; manda {grupos.Fuente}");

        var elemento = new ElementoLeido(pila[0].Id.ToString(), Nombre(pila, ejes, centro), ancho, alto, (int)Math.Round(LectorDespiece.Mm(zSup - zInf)), lista, EsColumna: true);
        return new ColumnaLeida(elemento, pila.Select(c => c.Id).ToList(), centro, zInf, zSup,
            varillas.Select((v, i) => new Varilla(v.Medida, v.Piezas, grupos?.Grupos[i])).ToList(), grupos?.Fuente);
    }

    /// <summary>
    /// Arma cada varilla con las piezas que traslapan una sobre otra (<see cref="CadenasVarillas.Unir"/>), no solo por
    /// cercanía: la pieza de arriba va corrida y en una columna circular quedaba junto a la vecina (hallazgo 31). El
    /// traslapo es donde una pieza pisa a la de abajo.
    /// </summary>
    private static List<(VarillaLeida, List<PiezaVarilla>)> Varillas(IEnumerable<Rebar> verticales)
    {
        var piezas = new List<(PiezaVarilla Pieza, XYZ Planta)>();
        foreach (Rebar b in verticales)
        {
            for (int i = 0; i < b.Quantity; i++)
            {
                Curve recta = b.GetTransformedCenterlineCurves(false, true, false, MultiplanarOption.IncludeOnlyPlanarCurves, i)
                    .OrderByDescending(c => c.Length).First();
                XYZ a = recta.GetEndPoint(0), e = recta.GetEndPoint(1);
                piezas.Add((new PiezaVarilla(b.Id, i, Math.Min(a.Z, e.Z), Math.Max(a.Z, e.Z)), new XYZ(a.X, a.Y, 0)));
            }
        }
        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir(piezas.Select(p =>
            new PiezaEnPlanta(p.Planta.X * 0.3048, p.Planta.Y * 0.3048, p.Pieza.ZInferior * 0.3048, p.Pieza.ZSuperior * 0.3048)).ToList());
        return cadenas.Select(c =>
        {
            List<PiezaVarilla> orden = c.Select(i => piezas[i].Pieza).ToList();
            var traslapos = new List<double>();
            for (int i = 1; i < orden.Count; i++)
                traslapos.Add((orden[i].ZInferior + orden[i - 1].ZSuperior) / 2 * 0.3048);
            XYZ planta = piezas[c[0]].Planta;
            return (new VarillaLeida(planta.X * 0.3048, planta.Y * 0.3048, traslapos), orden);
        }).ToList();
    }

    /// <summary>
    /// Cuántas varillas generó el add-in de columnas en cada columna, por <c>UniqueId</c> del elemento de abajo de la pila:
    /// 4 esquinas más las de cada cara (<c>ConfigLongitudinal.Anillo</c>). Vacío si no hay registro o no se entiende: el
    /// grupo sale entonces de la altura de los traslapos (decisión 4).
    /// </summary>
    private static Dictionary<string, int> VarillasDelRegistro(Document doc)
    {
        var varillas = new Dictionary<string, int>();
        try
        {
            if (Schema.Lookup(EsquemaColumnas) is not { } esquema)
                return varillas;
            Element? contenedor = new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).FirstOrDefault(e => e.GetEntity(esquema).IsValid());
            string? json = contenedor?.GetEntity(esquema).Get<string>(esquema.GetField("Json"));
            if (string.IsNullOrWhiteSpace(json))
                return varillas;
            using JsonDocument d = JsonDocument.Parse(json);
            foreach (JsonProperty p in d.RootElement.EnumerateObject())
                if (p.Value.TryGetProperty("Longitudinal", out JsonElement l)
                    && l.TryGetProperty("BarrasPorCaraX", out JsonElement x) && l.TryGetProperty("BarrasPorCaraY", out JsonElement y))
                    varillas[p.Name] = 4 + x.GetInt32() + y.GetInt32();
        }
        catch (Exception e)
        {
            // Un registro de otro formato no puede impedir el despiece.
            Registro.Paso("columnas: no se pudo leer el registro del add-in de columnas: " + e.Message);
        }
        return varillas;
    }

    /// <summary>
    /// Como la muestra Revit en la cuadrícula: la marca de ubicación ("Column Location Mark") del elemento más bajo de la
    /// pila que la tenga, tal cual, con la distancia al eje si la trae (decisión 15). Si Revit no da ninguna, con los ejes
    /// que se cruzan.
    /// </summary>
    private static string Nombre(List<FamilyInstance> pila, List<Grid> ejes, XYZ centro) =>
        pila.Select(c => NombreColumna.DeMarca(c.get_Parameter(BuiltInParameter.COLUMN_LOCATION_MARK)?.AsString() ?? ""))
            .FirstOrDefault(m => m.Length > 0) ?? Ejes(ejes, centro);

    /// <summary>
    /// Los ejes que se cruzan en la columna, la letra primero, con guion como la marca de Revit (<c>C-2</c>); si los dos son
    /// números, primero el que corre de sur a norte. Solo cuando Revit no da la marca.
    /// </summary>
    private static string Ejes(List<Grid> ejes, XYZ centro)
    {
        List<Grid> cerca = ejes.Where(g =>
        {
            var l = (Line)g.Curve;
            XYZ p = l.GetEndPoint(0), d = new XYZ(l.Direction.X, l.Direction.Y, 0).Normalize();
            return Math.Abs(new XYZ(centro.X - p.X, centro.Y - p.Y, 0).DotProduct(XYZ.BasisZ.CrossProduct(d))) < Cercania;
        }).OrderByDescending(g => Math.Abs(((Line)g.Curve).Direction.Y)).Take(2).ToList();
        if (cerca.Count < 2)
            return cerca.FirstOrDefault()?.Name ?? "";
        List<string> nombres = cerca.Select(g => g.Name).OrderBy(n => n.All(char.IsDigit)).ToList();
        return string.Join("-", nombres);
    }

    /// <summary>La Z más baja y la más alta del eje del conjunto, mirando la primera y la última barra.</summary>
    internal static (double Abajo, double Arriba) Alturas(Rebar b)
    {
        List<double> z = new[] { 0, Math.Max(0, b.Quantity - 1) }.Distinct()
            .SelectMany(i => b.GetTransformedCenterlineCurves(false, true, false, MultiplanarOption.IncludeOnlyPlanarCurves, i))
            .SelectMany(c => new[] { c.GetEndPoint(0).Z, c.GetEndPoint(1).Z }).ToList();
        return z.Count == 0 ? (0, 0) : (z.Min(), z.Max());
    }

    internal static bool EsVertical(Rebar b) => b.GetCenterlineCurves(false, true, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0)
        .OrderByDescending(c => c.Length).First() is Line l && Math.Abs(l.Direction.Z) > 0.99;

    /// <summary>
    /// Hacia dónde van sus ganchos en la sección: <c>A</c> si la punta se acerca al centro de la columna, <c>F</c> si se
    /// aleja; uno por gancho. Dos columnas iguales en todo menos en esto (una de esquina, junto al lindero, con los ganchos
    /// girados) no son iguales (decisión 19 del plan de #287).
    /// </summary>
    private static string LadoGanchos(Rebar b, XYZ centro)
    {
        IList<Curve> curvas = b.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
        if (curvas.Count < 3)
            return "";
        string Lado(XYZ dobles, XYZ punta)
        {
            XYZ pata = new XYZ(punta.X - dobles.X, punta.Y - dobles.Y, 0);
            XYZ alCentro = new XYZ(centro.X - dobles.X, centro.Y - dobles.Y, 0);
            return pata.DotProduct(alCentro) > 0 ? "A" : "F";
        }
        string lado = "";
        if (b.GetHookTypeId(0) != ElementId.InvalidElementId)
            lado += Lado(curvas[0].GetEndPoint(1), curvas[0].GetEndPoint(0));
        if (b.GetHookTypeId(1) != ElementId.InvalidElementId)
            lado += Lado(curvas[^1].GetEndPoint(0), curvas[^1].GetEndPoint(1));
        return lado;
    }

    private static XYZ CentroPlanta(FamilyInstance c)
    {
        BoundingBoxXYZ b = c.get_BoundingBox(null);
        return new XYZ((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2, 0);
    }
}
