namespace CQEING.Viguetas.Calculo;

/// <summary>Hacia dónde corren las viguetas: paralelas al eje X o al eje Y del proyecto.</summary>
public enum Direccion
{
    X,
    Y,
}

/// <summary>
/// Una pieza de vigueta de un panel. <paramref name="Cota"/> es su eje en la dirección del reparto; <paramref name="Desde"/>
/// y <paramref name="Hasta"/>, sus caras libres a lo largo (largo y tipo); <paramref name="EjeDesde"/> y
/// <paramref name="EjeHasta"/>, hasta los ejes de las vigas (la línea que se crea en Revit). Todo en mm.
/// <paramref name="Id"/> lleva el panel (<c>P4:3</c>, <c>P4:3a</c>, decisión 15). <paramref name="Tipo"/> es 0 si está
/// borrada. <paramref name="TopeDesde"/> y <paramref name="TopeHasta"/>: esa punta llega a una viga que termina dentro del
/// panel, y ahí la vigueta no sigue de largo (decisión 30).
/// </summary>
public sealed record Vigueta(string Id, string Panel, double Cota, double Desde, double Hasta, double EjeDesde, double EjeHasta, int Tipo, bool Borrada,
    bool TopeDesde = false, bool TopeHasta = false)
{
    public double Largo => Hasta - Desde;
}

/// <summary>Viguetas iguales (mismo largo libre, al cm) van en el mismo tipo; el 1 es el más largo.</summary>
public sealed record TipoVigueta(int Numero, double Largo, int Cantidad);

/// <summary>De dónde salen las líneas de las viguetas de un panel (decisiones 16 y 17).</summary>
public enum OrigenReparto
{
    /// <summary>La fórmula del Excel sobre el ancho libre del panel.</summary>
    Excel,

    /// <summary>Las mismas líneas que el primer panel elegido de su fila o columna (<see cref="RepartoPanel.Guia"/>).</summary>
    Alineado,

    /// <summary>Las líneas de las viguetas que ya están en la misma fila o columna.</summary>
    Existentes,
}

/// <summary>
/// Cómo quedó repartido un panel. <paramref name="Libre"/> es su ancho libre en la dirección del reparto, en mm.
/// <paramref name="Reparto"/>: con la fórmula, el del Excel; si no, la cantidad, el eje a eje más común y el libre del
/// primer extremo. <paramref name="LibreFinal"/> es el del otro extremo (igual al primero con la fórmula).
/// </summary>
public sealed record RepartoPanel(string Panel, double Libre, Reparto Reparto, OrigenReparto Origen = OrigenReparto.Excel,
    string? Guia = null, double? LibreFinal = null)
{
    public double LibreOtroExtremo => LibreFinal ?? Reparto.LibreExtremos;
}

/// <summary>
/// Una vigueta de borde de un lado libre de una zona o de un vacío (#954, decisión 4): <paramref name="Desde"/> y
/// <paramref name="Hasta"/> son su eje en planta, en mm.
/// </summary>
public sealed record ViguetaBorde(string Id, string Panel, Punto Desde, Punto Hasta, bool Borrada)
{
    public double Largo => Geometria.Distancia(Desde, Hasta);
}

/// <summary>
/// Lo que se propone para uno o varios paneles (#950, decisión 12). <paramref name="Bordes"/>: las viguetas de borde de los
/// lados libres de las zonas por puntos (#954).
/// </summary>
public sealed record PropuestaViguetas(Direccion Direccion, double Ancho, IReadOnlyList<RepartoPanel> Paneles,
    IReadOnlyList<Vigueta> Viguetas, IReadOnlyList<TipoVigueta> Tipos, IReadOnlyList<ViguetaBorde>? Bordes = null)
{
    public IReadOnlyList<ViguetaBorde> DeBorde => Bordes ?? [];

    /// <summary>Distancia (mm) a la que dos piezas se toman como unidas en el eje de una viga.</summary>
    public const double Union = 5;

    public IEnumerable<Vigueta> Vivas => Viguetas.Where(v => !v.Borrada);

    /// <summary>El punto en planta de una vigueta: <paramref name="largo"/> a lo largo de ella, a su cota.</summary>
    public Punto Punto(double cota, double largo) => Direccion == Direccion.X ? new(largo, cota) : new(cota, largo);

    /// <summary>
    /// Las líneas que se crean en Revit, de eje a eje de viga. Con <paramref name="continuas"/>, las piezas vivas en la
    /// misma línea que se tocan en el eje de una viga van juntas: una sola vigueta de punta a punta (decisión 13), salvo
    /// donde esa viga termina dentro del panel (decisión 30): ahí cada una llega a su eje y no se unen.
    /// </summary>
    public List<(Punto Desde, Punto Hasta)> Lineas(bool continuas)
    {
        var lineas = new List<(Punto, Punto)>();
        foreach (IGrouping<double, Vigueta> fila in Vivas.GroupBy(v => Math.Round(v.Cota)))
        {
            double cota = fila.First().Cota;
            (double Desde, double Hasta)? actual = null;
            bool tope = false;
            foreach (Vigueta v in fila.OrderBy(v => v.EjeDesde))
            {
                if (actual is (double d, double h) && continuas && !tope && !v.TopeDesde && Math.Abs(h - v.EjeDesde) <= Union)
                {
                    actual = (d, v.EjeHasta);
                    tope = v.TopeHasta;
                    continue;
                }
                tope = v.TopeHasta;
                if (actual is (double d0, double h0))
                    lineas.Add((Punto(cota, d0), Punto(cota, h0)));
                actual = (v.EjeDesde, v.EjeHasta);
            }
            if (actual is (double d1, double h1))
                lineas.Add((Punto(cota, d1), Punto(cota, h1)));
        }
        lineas.AddRange(DeBorde.Where(b => !b.Borrada).Select(b => (b.Desde, b.Hasta)));
        return lineas;
    }
}

/// <summary>
/// Reparte las viguetas de cada panel con la fórmula del Excel (<see cref="RepartoViguetas"/>) sobre su ancho libre en la
/// dirección del reparto, recorta cada una al contorno del panel y la parte donde la cruza una viga (decisión 14).
/// </summary>
public static class PlanificadorViguetas
{
    /// <summary>Piezas más cortas que esto (mm) no se proponen: quedan entre una viga que cruza y el borde.</summary>
    private const double PiezaMinima = 50;

    /// <summary>
    /// <paramref name="guias"/>: las cotas de las viguetas paralelas que ya están (<see cref="PisoViguetas.Guias"/>); con
    /// ellas, cada panel las sigue (decisiones 16 y 28). Null o vacío: la fórmula. <paramref name="ocupadas"/>: los tramos
    /// donde ya hay una vigueta del modelo (<see cref="PisoViguetas.PiezasDelModelo"/>); ahí no se propone otra.
    /// <paramref name="divisiones"/>: las vigas (<see cref="PisoViguetas.Vigas"/>); las paralelas a las viguetas que quedan
    /// dentro de un panel parten su reparto (#954, decisión 10).
    /// </summary>
    public static PropuestaViguetas? Planificar(IEnumerable<Panel> paneles, Direccion direccion, double ejeAEje, double ancho,
        IReadOnlySet<string> borradas, IEnumerable<BordePanel>? cruces = null, IReadOnlyList<double>? guias = null,
        IEnumerable<(Punto Desde, Punto Hasta)>? ocupadas = null, IEnumerable<BordePanel>? divisiones = null)
    {
        bool repartoEnY = direccion == Direccion.X;
        List<BordePanel> vigas = cruces?.ToList() ?? [];
        List<BordePanel> paralelas = (divisiones ?? []).Where(v => Paralela(v, repartoEnY)).ToList();
        // Los tramos ocupados en las coordenadas del reparto: cota y, a lo largo, desde y hasta.
        List<(double Cota, double Desde, double Hasta)> yaEstan = (ocupadas ?? [])
            .Where(o => repartoEnY ? Math.Abs(o.Hasta.Y - o.Desde.Y) < 20 : Math.Abs(o.Hasta.X - o.Desde.X) < 20)
            .Select(o => repartoEnY
                ? ((o.Desde.Y + o.Hasta.Y) / 2, Math.Min(o.Desde.X, o.Hasta.X), Math.Max(o.Desde.X, o.Hasta.X))
                : ((o.Desde.X + o.Hasta.X) / 2, Math.Min(o.Desde.Y, o.Hasta.Y), Math.Max(o.Desde.Y, o.Hasta.Y)))
            .ToList();
        var repartos = new List<RepartoPanel>();
        var piezas = new List<Vigueta>();
        (double Desde, double Hasta) Rango(Panel p) =>
            (p.Libre.Min(q => repartoEnY ? q.Y : q.X), p.Libre.Max(q => repartoEnY ? q.Y : q.X));

        foreach (List<Panel> linea in Lineas(paneles.ToList(), Rango))
        {
            Panel primero = linea[0];
            (double gd, double gh) = Rango(primero);
            List<double> puntas = Puntas(linea, vigas, repartoEnY, ancho);
            List<double> delPrimero = Repartir(gd, gh, puntas, ejeAEje, ancho, Bandas(primero, paralelas, repartoEnY));
            foreach (Panel panel in linea)
            {
                (double desde, double hasta) = Rango(panel);
                List<(double Desde, double Hasta)> bandas = Bandas(panel, paralelas, repartoEnY);
                // Decisión 10: ninguna vigueta encima de una viga paralela que queda dentro del panel.
                bool Libre(double c) => bandas.All(b => c + ancho / 2 <= b.Desde + 1 || c - ancho / 2 >= b.Hasta - 1);
                bool Entra(double c) => c - ancho / 2 >= desde - 1 && c + ancho / 2 <= hasta + 1 && Libre(c);
                List<double> existentes = guias?.Where(Entra).OrderBy(c => c).ToList() ?? [];
                List<double> alineadas = panel == primero ? [] : delPrimero.Where(Entra).ToList();
                List<double> cotas;
                RepartoPanel reparto;
                if (existentes.Count > 0 || alineadas.Count > 0)
                {
                    bool siguen = existentes.Count > 0;
                    cotas = siguen ? existentes : alineadas;
                    reparto = Resumir(panel.Id, desde, hasta, cotas, ejeAEje, ancho,
                        siguen ? OrigenReparto.Existentes : OrigenReparto.Alineado, siguen ? null : primero.Id);
                }
                else if (panel == primero && delPrimero.Count > 0 && (puntas.Any(Entra) || bandas.Count > 0))
                {
                    // Reparto parejo con una vigueta en la punta (decisión 31), o por tramos entre vigas paralelas.
                    cotas = delPrimero;
                    reparto = Resumir(panel.Id, desde, hasta, cotas, ejeAEje, ancho, OrigenReparto.Excel, null);
                }
                else if (bandas.Count > 0 && Repartir(desde, hasta, [], ejeAEje, ancho, bandas) is { Count: > 0 } porTramos)
                {
                    cotas = porTramos;
                    reparto = Resumir(panel.Id, desde, hasta, cotas, ejeAEje, ancho, OrigenReparto.Excel, null);
                }
                else if (RepartoViguetas.Calcular(hasta - desde, ejeAEje, ancho) is Reparto propio)
                {
                    cotas = Cotas(propio, desde, ancho);
                    reparto = new RepartoPanel(panel.Id, hasta - desde, propio);
                }
                else
                {
                    continue;
                }
                repartos.Add(reparto);
                for (int i = 0; i < cotas.Count; i++)
                    piezas.AddRange(Piezas(panel, cotas[i], i + 1, repartoEnY, vigas, borradas, ancho).Where(v => !YaEsta(v, yaEstan)));
            }
        }
        if (repartos.Count == 0)
            return null;
        List<Panel> todos = paneles.ToList();
        List<ViguetaBorde> bordes = todos.SelectMany(p => p.ViguetasDeBorde.Select((b, i) =>
            new ViguetaBorde($"{p.Id}:B{i + 1}", p.Id, b.Desde, b.Hasta, borradas.Contains($"{p.Id}:B{i + 1}")))).ToList();

        // Los tipos se agrupan entre todos los paneles elegidos.
        List<IGrouping<double, Vigueta>> grupos = piezas.Where(v => !v.Borrada)
            .GroupBy(v => Math.Round(v.Largo / 10)).OrderByDescending(g => g.Key).ToList();
        var numero = grupos.Select((g, i) => (g.Key, i + 1)).ToDictionary(x => x.Key, x => x.Item2);
        List<Vigueta> conTipo = piezas.Select(v => v.Borrada ? v : v with { Tipo = numero[Math.Round(v.Largo / 10)] }).ToList();
        List<TipoVigueta> tipos = grupos.Select((g, i) => new TipoVigueta(i + 1, g.Key * 10, g.Count())).ToList();
        return new PropuestaViguetas(direccion, ancho, repartos, conTipo, tipos, bordes);
    }

    /// <summary>
    /// Agrupa los paneles en filas o columnas (en el orden pedido): uno va con el primero de una línea si sus anchos en la
    /// dirección del reparto se superponen más de la mitad del más angosto (decisión 17).
    /// </summary>
    private static List<List<Panel>> Lineas(List<Panel> paneles, Func<Panel, (double Desde, double Hasta)> rango)
    {
        var lineas = new List<List<Panel>>();
        foreach (Panel p in paneles)
        {
            (double d, double h) = rango(p);
            List<Panel>? linea = lineas.FirstOrDefault(l =>
            {
                (double ld, double lh) = rango(l[0]);
                return Math.Min(h, lh) - Math.Max(d, ld) > 0.5 * Math.Min(h - d, lh - ld);
            });
            if (linea is null)
                lineas.Add([p]);
            else
                linea.Add(p);
        }
        return lineas;
    }

    /// <summary>Los ejes de las viguetas de un reparto del Excel que empieza en <paramref name="desde"/>.</summary>
    private static List<double> Cotas(Reparto? r, double desde, double ancho) => r is null
        ? []
        : Enumerable.Range(0, r.Cantidad).Select(i => desde + r.LibreExtremos + ancho / 2 + i * r.EjeAEje).ToList();

    /// <summary>
    /// Los ejes de las viguetas de [<paramref name="desde"/>, <paramref name="hasta"/>] (decisión 31). Sin puntas, la
    /// fórmula del Excel. Con puntas, el reparto parejo en el que una vigueta cae en la primera (<see cref="Parejo"/>) o,
    /// si no lo hay, la fórmula; y cada punta que no cae sobre una vigueta suma la suya, quitando la que quede pegada (a
    /// menos de dos anchos entre ejes).
    /// </summary>
    private static List<double> Repartir(double desde, double hasta, List<double> puntas, double ejeAEje, double ancho,
        List<(double Desde, double Hasta)> bandas)
    {
        // Decisión 10: cada tramo entre las caras de las vigas paralelas se reparte por su cuenta.
        var cotas = new List<double>();
        double inicio = desde;
        foreach ((double d, double h) in bandas.Append((hasta, hasta)))
        {
            if (Math.Min(d, hasta) - inicio >= ancho)
                cotas.AddRange(RepartirTramo(inicio, Math.Min(d, hasta), puntas, ejeAEje, ancho));
            inicio = Math.Max(inicio, h);
        }
        return cotas;
    }

    private static List<double> RepartirTramo(double desde, double hasta, List<double> puntas, double ejeAEje, double ancho)
    {
        var fijas = new List<double>();
        foreach (double c in puntas.Where(c => c - ancho / 2 >= desde - 1 && c + ancho / 2 <= hasta + 1))
            if (fijas.Count == 0 || c - fijas[^1] >= ancho)
                fijas.Add(c);
        Reparto? formula = RepartoViguetas.Calcular(hasta - desde, ejeAEje, ancho);
        List<double> cotas = (fijas.Count > 0 && formula is not null ? Parejo(desde, hasta, fijas[0], formula.Cantidad, ejeAEje, ancho) : null)
            ?? Cotas(formula, desde, ancho);
        List<double> faltan = fijas.Where(f => cotas.All(c => Math.Abs(c - f) > 1)).ToList();
        return cotas.Where(c => faltan.All(f => Math.Abs(f - c) >= 2 * ancho)).Concat(faltan).Order().ToList();
    }

    /// <summary>Cuántas viguetas más que la fórmula se prueban para que el reparto parejo caiga en la punta.</summary>
    private const int MasViguetas = 6;

    /// <summary>
    /// El reparto parejo con menos viguetas (desde <paramref name="minimo"/>) en el que una cae en <paramref name="punta"/>
    /// (decisión 31): misma separación s entre todas, no más que <paramref name="ejeAEje"/> ni menos que dos anchos, y los
    /// dos extremos iguales, de 0 al libre máximo. Con la de la punta en el lugar k de n:
    /// s = (libre − 2·t) / (n − 1 − 2k), con t del borde libre al eje de la punta. Null si no hay.
    /// </summary>
    private static List<double>? Parejo(double desde, double hasta, double punta, int minimo, double ejeAEje, double ancho)
    {
        double libre = hasta - desde, t = punta - desde, maximo = ejeAEje - ancho;
        for (int n = Math.Max(2, minimo); n <= minimo + MasViguetas; n++)
        {
            double? mejor = null;
            for (int k = 0; k < n; k++)
            {
                int den = n - 1 - 2 * k;
                // La punta en el centro: cualquier separación sirve; se usa el eje a eje elegido.
                double s = den == 0 ? (Math.Abs(libre - 2 * t) <= 1 ? ejeAEje : double.NaN) : (libre - 2 * t) / den;
                double extremo = (libre - ancho - (n - 1) * s) / 2;
                if (s >= 2 * ancho && s <= ejeAEje + 1e-6 && extremo >= -1e-6 && extremo <= maximo + 1e-6 && (mejor is null || s > mejor))
                    mejor = s;
            }
            if (mejor is double paso)
            {
                double extremo = (libre - ancho - (n - 1) * paso) / 2;
                return Enumerable.Range(0, n).Select(i => desde + extremo + ancho / 2 + i * paso).ToList();
            }
        }
        return null;
    }

    /// <summary>
    /// Los ejes, en la dirección del reparto, de las viguetas que van en las puntas de las vigas que terminan dentro de
    /// algún panel de la <paramref name="linea"/> (decisiones 30 y 31): por dentro de la viga, con la cara de la vigueta
    /// al ras de la punta. Las vigas paralelas a las viguetas no cuentan: no las cruzan.
    /// </summary>
    private static List<double> Puntas(List<Panel> linea, List<BordePanel> vigas, bool repartoEnY, double ancho)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        return vigas
            .Where(v => Math.Abs(U(v.Hasta) - U(v.Desde)) > 0.02 * Geometria.Distancia(v.Desde, v.Hasta))
            .SelectMany(v => new[] { (Punta: v.Desde, Otra: v.Hasta), (Punta: v.Hasta, Otra: v.Desde) })
            .Where(x => linea.Any(panel => Adentro(panel, x.Punta)))
            .Select(x => U(x.Punta) + Math.Sign(U(x.Otra) - U(x.Punta)) * ancho / 2)
            .Order().ToList();
    }

    /// <summary>Una viga es paralela a las viguetas si casi no avanza en la dirección del reparto.</summary>
    private static bool Paralela(BordePanel v, bool repartoEnY)
    {
        double du = repartoEnY ? v.Hasta.Y - v.Desde.Y : v.Hasta.X - v.Desde.X;
        return Math.Abs(du) <= 0.02 * Geometria.Distancia(v.Desde, v.Hasta);
    }

    /// <summary>
    /// Los cuerpos (en la dirección del reparto, de cara a cara) de las vigas paralelas que quedan dentro del ancho libre
    /// del panel y corren a lo largo de él al menos una pieza mínima, por dentro del panel (decisión 10). Ordenados.
    /// </summary>
    private static List<(double Desde, double Hasta)> Bandas(Panel panel, List<BordePanel> paralelas, bool repartoEnY)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        double W(Punto p) => repartoEnY ? p.X : p.Y;
        (double desde, double hasta) = (panel.Libre.Min(U), panel.Libre.Max(U));
        (double l0, double l1) = (panel.Libre.Min(W), panel.Libre.Max(W));
        var bandas = new List<(double, double)>();
        foreach (BordePanel v in paralelas)
        {
            double eje = (U(v.Desde) + U(v.Hasta)) / 2, medio = v.Ancho / 2;
            double w0 = Math.Max(l0, Math.Min(W(v.Desde), W(v.Hasta))), w1 = Math.Min(l1, Math.Max(W(v.Desde), W(v.Hasta)));
            if (eje + medio <= desde + 1 || eje - medio >= hasta - 1 || w1 - w0 < PiezaMinima)
                continue;
            Punto centro = repartoEnY ? new Punto((w0 + w1) / 2, eje) : new Punto(eje, (w0 + w1) / 2);
            if (Geometria.Dentro(panel.Eje, centro))
                bandas.Add((eje - medio, eje + medio));
        }
        return bandas.OrderBy(b => b.Item1).ToList();
    }

    /// <summary>Cantidad, eje a eje más común y los dos libres de extremo de unas líneas que no salen de la fórmula.</summary>
    private static RepartoPanel Resumir(string panel, double desde, double hasta, List<double> cotas, double ejeAEje, double ancho,
        OrigenReparto origen, string? guia)
    {
        List<double> pasos = cotas.Zip(cotas.Skip(1), (a, b) => Math.Round(b - a)).ToList();
        double eje = pasos.Count == 0 ? ejeAEje : pasos.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
        var r = new Reparto(cotas.Count, eje, cotas[0] - ancho / 2 - desde, ejeAEje - ancho);
        return new RepartoPanel(panel, hasta - desde, r, origen, guia, hasta - (cotas[^1] + ancho / 2));
    }

    /// <summary>Distancia (mm) entre ejes a la que una pieza propuesta se toma como la misma línea que una del modelo.</summary>
    private const double MismaLinea = 30;

    /// <summary>
    /// Si en la línea de <paramref name="v"/> ya hay una vigueta del modelo que cubre más de la mitad de su largo
    /// (decisión 28): entonces la pieza no se propone.
    /// </summary>
    private static bool YaEsta(Vigueta v, List<(double Cota, double Desde, double Hasta)> yaEstan) =>
        yaEstan.Any(o => Math.Abs(o.Cota - v.Cota) <= MismaLinea && Math.Min(o.Hasta, v.Hasta) - Math.Max(o.Desde, v.Desde) > v.Largo / 2);

    /// <summary>Las piezas de la vigueta <paramref name="numero"/> del panel, a su <paramref name="cota"/>.</summary>
    private static IEnumerable<Vigueta> Piezas(Panel panel, double cota, int numero, bool repartoEnY, List<BordePanel> vigas,
        IReadOnlySet<string> borradas, double ancho)
    {
        List<(double Desde, double Hasta)> ejes = Geometria.CortesAnillos(panel.AnillosApoyo, repartoEnY, cota);
        List<(double Desde, double Hasta)> tramos = Geometria.CortesAnillos(panel.AnillosLibre, repartoEnY, cota);
        for (int k = 0; k < tramos.Count; k++)
        {
            (double d, double h) = tramos[k];
            double medio = (d + h) / 2;
            (double ed, double eh) = ejes.FirstOrDefault(e => e.Desde <= medio && medio <= e.Hasta, (Desde: d, Hasta: h));
            string id = tramos.Count == 1 ? $"{panel.Id}:{numero}" : $"{panel.Id}:{numero}.{k + 1}";
            var partes = Partir(panel, d, h, ed, eh, cota, repartoEnY, vigas, ancho);
            for (int j = 0; j < partes.Count; j++)
            {
                string idParte = partes.Count == 1 ? id : $"{id}{(char)('a' + j)}";
                (double pd, double ph, double ped, double peh, bool td, bool th) = partes[j];
                yield return new Vigueta(idParte, panel.Id, cota, pd, ph, ped, peh, 0, borradas.Contains(idParte), td, th);
            }
        }
    }

    /// <summary>Una viga cuya punta queda a más de esto (mm) del borde del panel, adentro, termina dentro de él (decisión 30).</summary>
    private const double PuntaAdentro = 50;

    /// <summary>Si la punta <paramref name="p"/> de una viga queda dentro del panel y no en su borde (decisión 30).</summary>
    private static bool Adentro(Panel panel, Punto p) =>
        Geometria.Dentro(panel.Eje, p) && Geometria.DistanciaAlBorde(panel.Eje, p) > PuntaAdentro;

    /// <summary>
    /// Parte el tramo [<paramref name="d"/>, <paramref name="h"/>] donde lo cruza el eje de una viga: cada pieza llega a
    /// la cara de esa viga (largo y tipo) y a su eje (la línea de Revit). Si esa viga termina dentro del panel, las puntas
    /// de ese corte quedan como tope (decisión 30). La vigueta que va en la punta de una viga (a medio
    /// <paramref name="ancho"/> o menos) se parte igual, pero sin tope: se crea unida si no se quita ningún lado (decisión 31).
    /// </summary>
    private static List<(double D, double H, double ED, double EH, bool TopeD, bool TopeH)> Partir(Panel panel, double d, double h, double ed,
        double eh, double cota, bool repartoEnY, List<BordePanel> vigas, double ancho)
    {
        var cortes = new List<(double W, double Medio, bool Tope)>();
        foreach (BordePanel v in vigas)
        {
            (double ua, double ub) = repartoEnY ? (v.Desde.Y, v.Hasta.Y) : (v.Desde.X, v.Hasta.X);
            (double wa, double wb) = repartoEnY ? (v.Desde.X, v.Hasta.X) : (v.Desde.Y, v.Hasta.Y);
            if ((ua - cota) * (ub - cota) >= 0)
                continue;
            bool enPunta = Math.Min(Math.Abs(ua - cota), Math.Abs(ub - cota)) <= ancho / 2 + 1;
            double w = wa + (cota - ua) / (ub - ua) * (wb - wa);
            // Medio ancho medido a lo largo de la vigueta: más largo si la viga cruza en diagonal.
            double seno = Math.Abs(ub - ua) / Geometria.Distancia(v.Desde, v.Hasta);
            double medio = v.Ancho / 2 / seno;
            if (d + medio < w && w < h - medio)
                cortes.Add((w, medio, !enPunta && (Adentro(panel, v.Desde) || Adentro(panel, v.Hasta))));
        }

        var partes = new List<(double, double, double, double, bool, bool)>();
        (double desde, double ejeDesde, bool topeDesde) = (d, ed, false);
        foreach ((double w, double medio, bool tope) in cortes.OrderBy(c => c.W))
        {
            if (w - medio - desde >= PiezaMinima)
                partes.Add((desde, w - medio, ejeDesde, w, topeDesde, tope));
            (desde, ejeDesde, topeDesde) = (w + medio, w, tope);
        }
        if (h - desde >= PiezaMinima)
            partes.Add((desde, h, ejeDesde, eh, topeDesde, false));
        return partes;
    }
}
