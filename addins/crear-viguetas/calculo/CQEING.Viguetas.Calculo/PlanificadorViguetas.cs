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

/// <summary>Lo que se propone para uno o varios paneles (#950, decisión 12).</summary>
public sealed record PropuestaViguetas(Direccion Direccion, double Ancho, IReadOnlyList<RepartoPanel> Paneles,
    IReadOnlyList<Vigueta> Viguetas, IReadOnlyList<TipoVigueta> Tipos)
{
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
    /// </summary>
    public static PropuestaViguetas? Planificar(IEnumerable<Panel> paneles, Direccion direccion, double ejeAEje, double ancho,
        IReadOnlySet<string> borradas, IEnumerable<BordePanel>? cruces = null, IReadOnlyList<double>? guias = null,
        IEnumerable<(Punto Desde, Punto Hasta)>? ocupadas = null)
    {
        bool repartoEnY = direccion == Direccion.X;
        List<BordePanel> vigas = cruces?.ToList() ?? [];
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
            List<double> delPrimero = Cotas(RepartoViguetas.Calcular(gh - gd, ejeAEje, ancho), gd, ancho);
            foreach (Panel panel in linea)
            {
                (double desde, double hasta) = Rango(panel);
                bool Entra(double c) => c - ancho / 2 >= desde - 1 && c + ancho / 2 <= hasta + 1;
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
                    piezas.AddRange(Piezas(panel, cotas[i], i + 1, repartoEnY, vigas, borradas).Where(v => !YaEsta(v, yaEstan)));
            }
        }
        if (repartos.Count == 0)
            return null;

        // Los tipos se agrupan entre todos los paneles elegidos.
        List<IGrouping<double, Vigueta>> grupos = piezas.Where(v => !v.Borrada)
            .GroupBy(v => Math.Round(v.Largo / 10)).OrderByDescending(g => g.Key).ToList();
        var numero = grupos.Select((g, i) => (g.Key, i + 1)).ToDictionary(x => x.Key, x => x.Item2);
        List<Vigueta> conTipo = piezas.Select(v => v.Borrada ? v : v with { Tipo = numero[Math.Round(v.Largo / 10)] }).ToList();
        List<TipoVigueta> tipos = grupos.Select((g, i) => new TipoVigueta(i + 1, g.Key * 10, g.Count())).ToList();
        return new PropuestaViguetas(direccion, ancho, repartos, conTipo, tipos);
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
        IReadOnlySet<string> borradas)
    {
        List<(double Desde, double Hasta)> ejes = Geometria.Cortes(panel.Eje, repartoEnY, cota);
        List<(double Desde, double Hasta)> tramos = Geometria.Cortes(panel.Libre, repartoEnY, cota);
        for (int k = 0; k < tramos.Count; k++)
        {
            (double d, double h) = tramos[k];
            double medio = (d + h) / 2;
            (double ed, double eh) = ejes.FirstOrDefault(e => e.Desde <= medio && medio <= e.Hasta, (Desde: d, Hasta: h));
            string id = tramos.Count == 1 ? $"{panel.Id}:{numero}" : $"{panel.Id}:{numero}.{k + 1}";
            var partes = Partir(panel, d, h, ed, eh, cota, repartoEnY, vigas);
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

    /// <summary>
    /// Parte el tramo [<paramref name="d"/>, <paramref name="h"/>] donde lo cruza el eje de una viga: cada pieza llega a
    /// la cara de esa viga (largo y tipo) y a su eje (la línea de Revit). Si esa viga termina dentro del panel, las puntas
    /// de ese corte quedan como tope (decisión 30).
    /// </summary>
    private static List<(double D, double H, double ED, double EH, bool TopeD, bool TopeH)> Partir(Panel panel, double d, double h, double ed,
        double eh, double cota, bool repartoEnY, List<BordePanel> vigas)
    {
        bool Adentro(Punto p) => Geometria.Dentro(panel.Eje, p) && Geometria.DistanciaAlBorde(panel.Eje, p) > PuntaAdentro;
        var cortes = new List<(double W, double Medio, bool Tope)>();
        foreach (BordePanel v in vigas)
        {
            (double ua, double ub) = repartoEnY ? (v.Desde.Y, v.Hasta.Y) : (v.Desde.X, v.Hasta.X);
            (double wa, double wb) = repartoEnY ? (v.Desde.X, v.Hasta.X) : (v.Desde.Y, v.Hasta.Y);
            if ((ua - cota) * (ub - cota) >= 0)
                continue;
            double w = wa + (cota - ua) / (ub - ua) * (wb - wa);
            // Medio ancho medido a lo largo de la vigueta: más largo si la viga cruza en diagonal.
            double seno = Math.Abs(ub - ua) / Geometria.Distancia(v.Desde, v.Hasta);
            double medio = v.Ancho / 2 / seno;
            if (d + medio < w && w < h - medio)
                cortes.Add((w, medio, Adentro(v.Desde) || Adentro(v.Hasta)));
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
