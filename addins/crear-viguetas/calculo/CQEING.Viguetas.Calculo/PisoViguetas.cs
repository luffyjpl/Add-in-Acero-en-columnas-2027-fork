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

/// <summary>Un suelo del nivel (#954): sus contornos en planta, en mm (el primero es el de afuera).</summary>
public sealed record SueloPiso(string Id, IReadOnlyList<IReadOnlyList<Punto>> Contornos);

/// <summary>
/// Un punto que se puede elegir para armar una zona (#954). <paramref name="Tipo"/>: <c>columna</c>, <c>cruce</c>,
/// <c>viga</c>, <c>vigueta</c> o <c>suelo</c> (<see cref="PisoViguetas.Puntos"/>).
/// </summary>
public sealed record PuntoPiso(Punto P, string Tipo);

/// <summary>Una zona que armó el usuario con puntos (#954): su contorno y sus vacíos, por ejes, en mm.</summary>
public sealed record ZonaPuntos(string Id, IReadOnlyList<Punto> Contorno, IReadOnlyList<IReadOnlyList<Punto>> Vacios);

/// <summary>
/// Lo que se lee de un nivel de Revit para «Crear viguetas» (#948). <paramref name="Cota"/> en mm. <paramref name="Suelos"/>:
/// los suelos del nivel (#954).
/// </summary>
public sealed record PisoLeido(string Id, string Nombre, double Cota, IReadOnlyList<ArmazonPiso> Armazon, IReadOnlyList<ColumnaPiso> Columnas,
    IReadOnlyList<SueloPiso>? Suelos = null)
{
    public IReadOnlyList<SueloPiso> LosSuelos => Suelos ?? [];
}

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

    public Panel? Panel(string id) =>
        Paneles.FirstOrDefault(p => p.Id == id) ?? perimetros.FirstOrDefault(p => p.Id == id) ?? zonas.FirstOrDefault(p => p.Id == id);

    private List<Panel> perimetros = [];

    /// <summary>
    /// Arma el perímetro de trabajo con los elementos de armazón que eligió el usuario (decisión 24): vigas, viguetas de
    /// borde o voladizos, menos los creados por el add-in. Cada cara cerrada es un perímetro (Z1, Z2…) y reemplaza a los
    /// anteriores y a las zonas por puntos (usan los mismos ids). Vacío si no cierran.
    /// </summary>
    public IReadOnlyList<Panel> Perimetro(IEnumerable<string> ids)
    {
        HashSet<string> elegidos = ids.ToHashSet();
        zonas = [];
        perimetros = DetectorPaneles.Detectar(Piso.Armazon.Where(a => elegidos.Contains(a.Id) && !a.Creada)
                .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)))
            .Select((p, i) => p with { Id = $"Z{i + 1}" }).ToList();
        return perimetros;
    }

    /// <summary>A menos de esto (mm), dos puntos que se pueden elegir son el mismo (#954).</summary>
    private const double MismoPunto = 30;

    /// <summary>
    /// Los puntos que se pueden elegir para armar zonas (#954, decisión 2): centro de cada columna, cruces de ejes del
    /// armazón, extremos del armazón, puntas de las viguetas que creó el add-in (decisión 24) y vértices de los suelos, en
    /// ese orden de preferencia. Lo creado por el add-in no da cruces. Tipos: <c>columna</c>, <c>cruce</c> (de dos vigas), <c>viga</c> (extremo de viga), <c>vigueta</c>
    /// (extremo de vigueta o cruce con una) y <c>suelo</c>.
    /// </summary>
    public IReadOnlyList<PuntoPiso> Puntos => puntos ??= ArmarPuntos();

    private IReadOnlyList<PuntoPiso>? puntos;

    private List<PuntoPiso> ArmarPuntos()
    {
        var lista = new List<PuntoPiso>();
        void Sumar(Punto p, string tipo)
        {
            if (lista.All(x => Geometria.Distancia(x.P, p) > MismoPunto))
                lista.Add(new PuntoPiso(new Punto(Math.Round(p.X, 1), Math.Round(p.Y, 1)), tipo));
        }
        foreach (ColumnaPiso c in Piso.Columnas)
            Sumar(new Punto((c.Min.X + c.Max.X) / 2, (c.Min.Y + c.Max.Y) / 2), "columna");
        List<ArmazonPiso> propio = Piso.Armazon.Where(a => !a.Creada).ToList();
        for (int i = 0; i < propio.Count; i++)
            for (int j = i + 1; j < propio.Count; j++)
                if (Cruce(propio[i], new BordePanel(propio[j].Id, propio[j].Desde, propio[j].Hasta, propio[j].Ancho), incluirPuntas: true) is double t)
                    Sumar(propio[i].En(t), propio[i].EsVigueta || propio[j].EsVigueta ? "vigueta" : "cruce");
        foreach (ArmazonPiso a in propio)
        {
            Sumar(a.Desde, a.EsVigueta ? "vigueta" : "viga");
            Sumar(a.Hasta, a.EsVigueta ? "vigueta" : "viga");
        }
        // Decisión 24: las puntas de las viguetas que creó el add-in también se pueden elegir.
        foreach (ArmazonPiso a in Piso.Armazon.Where(a => a.Creada))
        {
            Sumar(a.Desde, "vigueta");
            Sumar(a.Hasta, "vigueta");
        }
        foreach (SueloPiso s in Piso.LosSuelos)
            foreach (Punto p in s.Contornos.SelectMany(c => c))
                Sumar(p, "suelo");
        return lista;
    }

    /// <summary>Un lado de una zona va sobre un elemento si las puntas del elemento están a medio ancho más esto (mm), o menos, de la línea del lado.</summary>
    private const double SobreElEje = 30;

    private List<Panel> zonas = [];

    /// <summary>
    /// Arma las zonas por puntos (#954, decisiones 4 a 6) y reemplaza a las anteriores. Los lados que van sobre un
    /// elemento de armazón se corren medio ancho de ese elemento; los lados libres llevan una vigueta de borde de
    /// <paramref name="ancho"/> con la cara al ras del lado, hacia adentro de la losa. Los vacíos se restan de la zona
    /// con la misma regla. Borra los perímetros «Por vigas»: usan los mismos ids.
    /// </summary>
    public IReadOnlyList<Panel> Zonas(IEnumerable<ZonaPuntos> pedidas, double ancho)
    {
        perimetros = [];
        zonas = pedidas.Where(z => z.Contorno.Count >= 3).Select(z => Zona(z, ancho)).OfType<Panel>().ToList();
        return zonas;
    }

    private Panel? Zona(ZonaPuntos z, double ancho)
    {
        List<Punto> eje = Orientar(Enderezar(z.Contorno.Select(AlEje).ToList()), antihorario: true);
        (List<Punto> libre, List<Punto> apoyo, List<(Punto, Punto)> bordes) = Correr(eje, ancho);
        if (libre.Count < 3 || Geometria.Area(libre) <= 0)
            return null;
        var huecosEje = new List<IReadOnlyList<Punto>>();
        var huecosLibre = new List<IReadOnlyList<Punto>>();
        var apoyos = new List<IReadOnlyList<Punto>> { apoyo };
        var bordesVacios = new List<(Punto, Punto)>();
        foreach (IReadOnlyList<Punto> v in z.Vacios.Where(v => v.Count >= 3))
        {
            // Horario: correr «hacia adentro» del anillo agranda el vacío, o sea, va hacia adentro de la losa.
            List<Punto> hueco = Orientar(Enderezar(v.Select(AlEje).ToList()), antihorario: false);
            (List<Punto> huecoLibre, List<Punto> huecoApoyo, List<(Punto, Punto)> bordesHueco) = Correr(hueco, ancho);
            huecosEje.Add(hueco);
            huecosLibre.Add(huecoLibre);
            apoyos.Add(huecoApoyo);
            bordesVacios.AddRange(bordesHueco);
        }
        return new Panel(z.Id, eje, libre, huecosEje, huecosLibre, bordes, apoyos, bordesVacios);
    }

    /// <summary>Un lado va «casi derecho» si se inclina esto (tangente, unos 5°) o menos respecto de X o de Y.</summary>
    private const double CasiDerecho = 0.0875;

    /// <summary>
    /// Decisión 20 (sin diagonales): cada lado casi horizontal toma una sola y, y cada lado casi vertical una sola x (el
    /// promedio de los lados casi derechos seguidos en la misma dirección). Los lados más inclinados quedan como están.
    /// Después se quitan los vértices repetidos o en línea.
    /// </summary>
    private static List<Punto> Enderezar(List<Punto> p)
    {
        int n = p.Count;
        if (n < 3)
            return p;
        // Para cada lado: 'H' casi horizontal, 'V' casi vertical, ' ' inclinado.
        char Tipo(int i)
        {
            Punto a = p[i], b = p[(i + 1) % n];
            double dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
            return dy <= CasiDerecho * dx ? 'H' : dx <= CasiDerecho * dy ? 'V' : ' ';
        }
        char[] tipos = Enumerable.Range(0, n).Select(Tipo).ToArray();
        double[] xs = p.Select(q => q.X).ToArray(), ys = p.Select(q => q.Y).ToArray();
        foreach (char t in "HV")
        {
            // Cadenas de lados seguidos del mismo tipo: todos sus vértices toman el promedio.
            bool[] visto = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (tipos[i] != t || visto[i])
                    continue;
                int inicio = i;
                while (tipos[(inicio - 1 + n) % n] == t && (inicio - 1 + n) % n != i)
                    inicio = (inicio - 1 + n) % n;
                var vertices = new List<int>();
                int j = inicio;
                do
                {
                    visto[j] = true;
                    vertices.Add(j);
                    j = (j + 1) % n;
                }
                while (tipos[(j - 1 + n) % n] == t && tipos[j] == t && j != inicio);
                vertices.Add(j);
                double medio = vertices.Average(k => t == 'H' ? p[k].Y : p[k].X);
                foreach (int k in vertices)
                    if (t == 'H')
                        ys[k] = medio;
                    else
                        xs[k] = medio;
            }
        }
        var r = new List<Punto>();
        for (int i = 0; i < n; i++)
        {
            var q = new Punto(xs[i], ys[i]);
            if (r.Count == 0 || Geometria.Distancia(r[^1], q) > 1)
                r.Add(q);
        }
        if (r.Count > 1 && Geometria.Distancia(r[0], r[^1]) <= 1)
            r.RemoveAt(r.Count - 1);
        return r.Count >= 3 ? Geometria.SinColineales(r) : r;
    }

    /// <summary>
    /// Decisión 13: un punto que cae dentro del cuerpo de un elemento del armazón (a medio ancho + <see cref="SobreElEje"/>
    /// de su eje, o menos, y entre sus puntas) se corre al eje del más cercano. Así un vértice del suelo pegado a una viga
    /// no deja un lado inclinado que la cruce con una vigueta de borde encima.
    /// </summary>
    private Punto AlEje(Punto p)
    {
        Punto mejor = p;
        double menor = double.MaxValue;
        foreach (ArmazonPiso x in Piso.Armazon.Where(x => !x.Creada))
        {
            double largo = Geometria.Distancia(x.Desde, x.Hasta);
            if (largo < 1)
                continue;
            (double ux, double uy) = ((x.Hasta.X - x.Desde.X) / largo, (x.Hasta.Y - x.Desde.Y) / largo);
            double a = (p.X - x.Desde.X) * ux + (p.Y - x.Desde.Y) * uy;
            if (a < -SobreElEje || a > largo + SobreElEje)
                continue;
            var q = new Punto(x.Desde.X + a * ux, x.Desde.Y + a * uy);
            double d = Geometria.Distancia(p, q);
            if (d <= x.Ancho / 2 + SobreElEje && d < menor)
                (mejor, menor) = (q, d);
        }
        return mejor;
    }

    /// <summary>
    /// Corre cada lado de <paramref name="anillo"/> hacia su izquierda: medio ancho del elemento sobre el que va o, si es un
    /// lado libre, un <paramref name="ancho"/> de vigueta, y en ese caso suma la vigueta de borde (su eje a medio ancho).
    /// También devuelve el anillo de apoyo (decisión 4): los lados sobre armazón quedan en su eje y los libres, en el eje
    /// de su vigueta de borde.
    /// </summary>
    private (List<Punto> Libre, List<Punto> Apoyo, List<(Punto, Punto)> Bordes) Correr(List<Punto> anillo, double ancho)
    {
        var corrimientos = new List<double>();
        var alApoyo = new List<double>();
        var bordes = new List<(Punto, Punto)>();
        for (int i = 0; i < anillo.Count; i++)
        {
            Punto a = anillo[i], b = anillo[(i + 1) % anillo.Count];
            if (Debajo(a, b) is (double cara, double eje))
            {
                corrimientos.Add(cara);
                alApoyo.Add(eje);
                continue;
            }
            corrimientos.Add(ancho);
            alApoyo.Add(ancho / 2);
            double largo = Geometria.Distancia(a, b);
            (double nx, double ny) = (-(b.Y - a.Y) / largo * ancho / 2, (b.X - a.X) / largo * ancho / 2);
            bordes.Add((new Punto(a.X + nx, a.Y + ny), new Punto(b.X + nx, b.Y + ny)));
        }
        return (Geometria.HaciaAdentro(anillo, corrimientos), Geometria.HaciaAdentro(anillo, alApoyo), bordes);
    }

    /// <summary>Un elemento cuenta como paralelo a un lado si el ángulo entre los dos es de esto (seno, unos 2°) o menos.</summary>
    private const double Paralelo = 0.035;

    /// <summary>
    /// Si el lado a–b va sobre armazón (decisión 4): cuánto se corre el libre hacia la izquierda del lado (hasta la cara de
    /// adentro del elemento) y cuánto el apoyo (hasta su eje). Null si es un lado libre. Cuentan los elementos paralelos al
    /// lado con sus dos puntas a no más de medio ancho + 30 mm de su línea (el lado pasa por el cuerpo del elemento,
    /// aunque no por su eje), y entre todos tienen que cubrir más de la mitad del lado (las vigas van una por vano). Si son
    /// varios, manda el de la cara más adentro.
    /// </summary>
    private (double Cara, double Eje)? Debajo(Punto a, Punto b)
    {
        double lado = Geometria.Distancia(a, b);
        if (lado < 1)
            return null;
        (double ux, double uy) = ((b.X - a.X) / lado, (b.Y - a.Y) / lado);
        // Distancia con signo a la línea del lado: positiva a su izquierda, hacia adentro de la losa.
        double Izquierda(Punto p) => -(p.X - a.X) * uy + (p.Y - a.Y) * ux;
        double A(Punto p) => (p.X - a.X) * ux + (p.Y - a.Y) * uy;
        var tramos = new List<(double Desde, double Hasta)>();
        (double Cara, double Eje)? debajo = null;
        foreach (ArmazonPiso x in Piso.Armazon.Where(x => !x.Creada))
        {
            double largo = Geometria.Distancia(x.Desde, x.Hasta);
            if (largo < 1 || Math.Abs(((x.Hasta.X - x.Desde.X) * uy - (x.Hasta.Y - x.Desde.Y) * ux) / largo) > Paralelo)
                continue;
            double s0 = Izquierda(x.Desde), s1 = Izquierda(x.Hasta), tope = x.Ancho / 2 + SobreElEje;
            if (Math.Abs(s0) > tope || Math.Abs(s1) > tope)
                continue;
            double desde = Math.Max(0, Math.Min(A(x.Desde), A(x.Hasta))), hasta = Math.Min(lado, Math.Max(A(x.Desde), A(x.Hasta)));
            if (hasta - desde < 1)
                continue;
            tramos.Add((desde, hasta));
            double eje = (s0 + s1) / 2, cara = eje + x.Ancho / 2;
            if (debajo is not (double c, _) || cara > c)
                debajo = (cara, eje);
        }
        if (debajo is not (double caraAdentro, double ejeApoyo) || Unir(tramos).Sum(t => t.Hasta - t.Desde) <= lado / 2)
            return null;
        return (Math.Max(0, caraAdentro), ejeApoyo);
    }

    private static List<Punto> Orientar(IReadOnlyList<Punto> p, bool antihorario) =>
        (Geometria.Area(p) > 0) == antihorario ? [.. p] : [.. p.Reverse()];

    /// <summary>
    /// Lo que puede atravesar una vigueta (decisión 14): todo el armazón del piso menos lo que creó el add-in. Los bordes
    /// del panel no la cortan: lo cruzan fuera del tramo libre.
    /// </summary>
    public IReadOnlyList<BordePanel> Cruces { get; } = piso.Armazon.Where(a => !a.Creada)
        .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)).ToList();

    /// <summary>
    /// Las vigas del piso (armazón propio que no es vigueta): las paralelas a las viguetas que quedan dentro de una zona
    /// parten su reparto (#954, decisión 10).
    /// </summary>
    public IReadOnlyList<BordePanel> Vigas { get; } = piso.Armazon.Where(a => !a.Creada && !a.EsVigueta)
        .Select(a => new BordePanel(a.Id, a.Desde, a.Hasta, a.Ancho)).ToList();

    /// <summary>Viguetas de la misma línea a menos de esto (mm) se toman como una sola guía.</summary>
    private const double JuntarGuias = 20;

    /// <summary>
    /// Las cotas de las viguetas que ya están (del modelo o creadas por el add-in), paralelas a
    /// <paramref name="direccion"/>, dentro o fuera de lo elegido (decisiones 16 y 28): las viguetas nuevas van en esas
    /// mismas líneas. El planificador usa solo las que caen dentro de cada panel. Las creadas por el add-in dentro de
    /// <paramref name="elegidos"/> no cuentan: «Crear» las va a reemplazar (#954, decisión 21).
    /// </summary>
    public List<double> Guias(Direccion direccion, IEnumerable<Panel>? elegidos = null)
    {
        bool enX = direccion == Direccion.X;
        List<Panel> reemplazo = elegidos?.ToList() ?? [];
        bool SeReemplaza(ArmazonPiso a) => a.Creada && reemplazo.Any(p => Geometria.Dentro(p.Eje, a.En(0.5)));
        List<double> cotas = Piso.Armazon
            .Where(a => (a.EsVigueta || a.Creada) && Paralela(a, enX) && !SeReemplaza(a))
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

    /// <summary>
    /// Dónde cruza el eje de <paramref name="otra"/> a <paramref name="a"/>, como fracción de su largo. Con
    /// <paramref name="incluirPuntas"/>, también si se tocan en una punta (para los puntos del piso).
    /// </summary>
    private static double? Cruce(ArmazonPiso a, BordePanel otra, bool incluirPuntas = false)
    {
        (double dx, double dy) = (a.Hasta.X - a.Desde.X, a.Hasta.Y - a.Desde.Y);
        (double ex, double ey) = (otra.Hasta.X - otra.Desde.X, otra.Hasta.Y - otra.Desde.Y);
        double det = dx * ey - dy * ex;
        if (Math.Abs(det) < 1e-6 * Math.Sqrt((dx * dx + dy * dy) * (ex * ex + ey * ey)))
            return null;
        (double fx, double fy) = (otra.Desde.X - a.Desde.X, otra.Desde.Y - a.Desde.Y);
        double t = (fx * ey - fy * ex) / det, u = (fx * dy - fy * dx) / det;
        double margen = incluirPuntas ? 1e-6 : 0;
        return t > -margen && t < 1 + margen && u >= -margen && u <= 1 + margen && (incluirPuntas || (t > 0 && t < 1)) ? t : null;
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
