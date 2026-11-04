using CQEING.Vigas.Calculo.Porticos;

namespace CQEING.Viguetas.Calculo;

/// <summary>
/// Un elemento de armazón del piso, en mm. <paramref name="Creada"/>: la creó «Crear viguetas» (tiene la marca en
/// Comentarios).
/// </summary>
public sealed record ArmazonPiso(string Id, Punto Desde, Punto Hasta, double Ancho, string? NombreTipo, bool Creada)
{
    public bool EsVigueta => ReglaVigueta.Es(NombreTipo, Ancho);

    /// <summary>El punto a <paramref name="t"/> del largo (0 = Desde, 1 = Hasta).</summary>
    public Punto En(double t) => new(Desde.X + (Hasta.X - Desde.X) * t, Desde.Y + (Hasta.Y - Desde.Y) * t);
}

/// <summary>
/// Una pieza de una vigueta del modelo dentro de los paneles elegidos (decisión 22): <paramref name="Desde"/> y
/// <paramref name="Hasta"/> son fracciones del largo del elemento; <paramref name="Inicio"/> y <paramref name="Fin"/>, sus
/// puntos en planta (mm), para dibujarla.
/// </summary>
public sealed record PiezaModelo(string Id, string Elemento, double Desde, double Hasta, Punto Inicio, Punto Fin);

/// <summary>Una columna del piso como su caja en planta, en mm.</summary>
public sealed record ColumnaPiso(string Id, Punto Min, Punto Max);

/// <summary>Lo que se lee de un nivel de Revit para «Crear viguetas» (#948). <paramref name="Cota"/> en mm.</summary>
public sealed record PisoLeido(string Id, string Nombre, double Cota, IReadOnlyList<ArmazonPiso> Armazon, IReadOnlyList<ColumnaPiso> Columnas);

/// <summary>Un nivel en la lista de pisos: cuántos elementos de armazón tiene.</summary>
public sealed record PisoResumen(string Id, string Nombre, double Cota, int Armazon);

/// <summary>Un tipo de armazón del modelo que se puede usar como vigueta; <paramref name="Sugerido"/> si cumple la regla de vigueta.</summary>
public sealed record TipoArmazon(string Id, string Nombre, bool Sugerido);

/// <summary>
/// El piso abierto en la ventana y sus paneles (#947). Las viguetas que ya existen no parten los paneles salvo que se
/// pida (<paramref name="viguetasComoBorde"/>, para las viguetas de borde); las creadas por el add-in nunca.
/// </summary>
public sealed class PisoViguetas(PisoLeido piso, bool viguetasComoBorde)
{
    public PisoLeido Piso { get; } = piso;

    public bool ViguetasComoBorde { get; } = viguetasComoBorde;

    public IReadOnlyList<Panel> Paneles { get; } = DetectorPaneles.Detectar(piso.Armazon
        .Where(a => !a.Creada && (viguetasComoBorde || !a.EsVigueta))
        .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)));

    public Panel? Panel(string id) => Paneles.FirstOrDefault(p => p.Id == id) ?? perimetros.FirstOrDefault(p => p.Id == id);

    private List<Panel> perimetros = [];

    /// <summary>
    /// Arma el perímetro de trabajo con los elementos de armazón que eligió el usuario (decisión 24): vigas, viguetas de
    /// borde o voladizos, menos los creados por el add-in. Cada cara cerrada es un perímetro (Z1, Z2…) y reemplaza a los
    /// anteriores. Vacío si no cierran.
    /// </summary>
    public IReadOnlyList<Panel> Perimetro(IEnumerable<string> ids)
    {
        HashSet<string> elegidos = ids.ToHashSet();
        perimetros = DetectorPaneles.Detectar(Piso.Armazon.Where(a => elegidos.Contains(a.Id) && !a.Creada)
                .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)))
            .Select((p, i) => p with { Id = $"Z{i + 1}" }).ToList();
        return perimetros;
    }

    /// <summary>
    /// Lo que puede atravesar una vigueta (decisión 14): todo el armazón del piso menos lo que creó el add-in. Los bordes
    /// del panel no la cortan: lo cruzan fuera del tramo libre.
    /// </summary>
    public IReadOnlyList<BordePanel> Cruces { get; } = piso.Armazon.Where(a => !a.Creada)
        .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)).ToList();

    /// <summary>Viguetas de la misma línea a menos de esto (mm) se toman como una sola guía.</summary>
    private const double JuntarGuias = 20;

    /// <summary>
    /// Las cotas de las viguetas que ya están (del modelo o creadas por el add-in), paralelas a
    /// <paramref name="direccion"/>, dentro o fuera de lo elegido (decisiones 16 y 28): las viguetas nuevas van en esas
    /// mismas líneas. El planificador usa solo las que caen dentro de cada panel.
    /// </summary>
    public List<double> Guias(Direccion direccion)
    {
        bool enX = direccion == Direccion.X;
        List<double> cotas = Piso.Armazon
            .Where(a => (a.EsVigueta || a.Creada) && Paralela(a, enX))
            .Select(a => enX ? a.En(0.5).Y : a.En(0.5).X)
            .OrderBy(c => c).ToList();
        var juntas = new List<double>();
        foreach (double c in cotas)
            if (juntas.Count == 0 || c - juntas[^1] > JuntarGuias)
                juntas.Add(c);
        return juntas;
    }

    /// <summary>Tramos (mm) de menos de esto no se dejan como resto ni como pieza: quedarían pegados a la viga.</summary>
    private const double RestoMinimo = 50;

    /// <summary>
    /// Las piezas de las viguetas del modelo dentro de <paramref name="paneles"/> (decisión 22): cada vigueta (que cumple
    /// la regla y no la creó el add-in) se toma solo en su tramo dentro de los paneles, por ejes, y ese tramo se parte
    /// donde lo cruza otro elemento de armazón (una viga). Cada pieza se quita por separado, como las propuestas.
    /// </summary>
    public List<PiezaModelo> PiezasDelModelo(IEnumerable<Panel> paneles)
    {
        List<Panel> lista = paneles.ToList();
        var piezas = new List<PiezaModelo>();
        foreach (ArmazonPiso a in Piso.Armazon.Where(a => a.EsVigueta && !a.Creada))
        {
            double largo = Geometria.Distancia(a.Desde, a.Hasta);
            List<double> cortes = Cruces.Where(c => c.Id != a.Id).Select(c => Cruce(a, c)).OfType<double>().OrderBy(t => t).ToList();
            int numero = 0;
            foreach ((double d, double h) in Unir(lista.SelectMany(p => Geometria.TramosDentro(a.Desde, a.Hasta, p.Eje))))
            {
                var limites = new List<double> { d };
                limites.AddRange(cortes.Where(t => t > d && t < h));
                limites.Add(h);
                for (int i = 0; i + 1 < limites.Count; i++)
                    if ((limites[i + 1] - limites[i]) * largo >= RestoMinimo)
                        piezas.Add(new PiezaModelo($"{a.Id}#{++numero}", a.Id, limites[i], limites[i + 1], a.En(limites[i]), a.En(limites[i + 1])));
            }
        }
        return piezas;
    }

    /// <summary>El elemento al que pertenece el id de una pieza (<c>123456#2</c> → <c>123456</c>).</summary>
    public static string ElementoDe(string pieza) => pieza.Split('#')[0];

    public ArmazonPiso? Elemento(string id) => Piso.Armazon.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Lo que queda de <paramref name="a"/> sin las piezas <paramref name="quitadas"/> (decisiones 21 y 22): fracciones
    /// [desde, hasta] de su largo. Van en fracciones para aplicarlas a la línea de ubicación de Revit, que puede ir corrida
    /// del centro (justificación).
    /// </summary>
    public static List<(double Desde, double Hasta)> Restos(ArmazonPiso a, IEnumerable<(double Desde, double Hasta)> quitadas)
    {
        double largo = Geometria.Distancia(a.Desde, a.Hasta);
        var restos = new List<(double, double)>();
        double desde = 0;
        foreach ((double d, double h) in Unir(quitadas))
        {
            restos.Add((desde, d));
            desde = h;
        }
        restos.Add((desde, 1));
        return restos.Where(t => (t.Item2 - t.Item1) * largo >= RestoMinimo).ToList();
    }

    /// <summary>Dónde cruza el eje de <paramref name="otra"/> a <paramref name="a"/>, como fracción de su largo.</summary>
    private static double? Cruce(ArmazonPiso a, BordePanel otra)
    {
        (double dx, double dy) = (a.Hasta.X - a.Desde.X, a.Hasta.Y - a.Desde.Y);
        (double ex, double ey) = (otra.Hasta.X - otra.Desde.X, otra.Hasta.Y - otra.Desde.Y);
        double det = dx * ey - dy * ex;
        if (Math.Abs(det) < 1e-6 * Math.Sqrt((dx * dx + dy * dy) * (ex * ex + ey * ey)))
            return null;
        (double fx, double fy) = (otra.Desde.X - a.Desde.X, otra.Desde.Y - a.Desde.Y);
        double t = (fx * ey - fy * ex) / det, u = (fx * dy - fy * dx) / det;
        return t > 0 && t < 1 && u >= 0 && u <= 1 ? t : null;
    }

    /// <summary>Une tramos [desde, hasta] que se tocan o se superponen.</summary>
    private static List<(double Desde, double Hasta)> Unir(IEnumerable<(double Desde, double Hasta)> tramos)
    {
        var unidos = new List<(double Desde, double Hasta)>();
        foreach ((double d, double h) in tramos.OrderBy(t => t.Desde))
        {
            if (unidos.Count > 0 && d <= unidos[^1].Hasta + 1e-6)
                unidos[^1] = (unidos[^1].Desde, Math.Max(unidos[^1].Hasta, h));
            else
                unidos.Add((d, h));
        }
        return unidos;
    }

    private static bool Paralela(ArmazonPiso a, bool enX)
    {
        double dx = Math.Abs(a.Hasta.X - a.Desde.X), dy = Math.Abs(a.Hasta.Y - a.Desde.Y);
        return enX ? dy <= 0.02 * dx : dx <= 0.02 * dy;
    }

    /// <summary>Los paneles con esos ids que existen en el piso, en el orden pedido.</summary>
    public List<Panel> PanelesDe(IEnumerable<string> ids) => ids.Select(Panel).OfType<Panel>().ToList();

    /// <summary>
    /// Las viguetas que creó el add-in y pasan por el panel: alguno de sus puntos (cada 5 % del largo) cae dentro. Una
    /// vigueta continua de varios paneles (decisión 13) cuenta en todos los que cruza.
    /// </summary>
    public List<ArmazonPiso> CreadasEn(Panel panel) =>
        Piso.Armazon.Where(a => a.Creada && Enumerable.Range(1, 19).Any(i => Geometria.Dentro(panel.Eje, a.En(i / 20.0)))).ToList();
}
