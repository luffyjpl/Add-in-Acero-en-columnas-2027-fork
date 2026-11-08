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
    string? Guia = null, double? LibreFinal = null, RegionReparto? Region = null)
{
    public double LibreOtroExtremo => LibreFinal ?? Reparto.LibreExtremos;
}

/// <summary>
/// Una región de una zona que se reparte por su cuenta (#954, decisión 18): de <paramref name="Desde"/> a
/// <paramref name="Hasta"/> en la dirección del reparto (de cara a cara) y de <paramref name="WDesde"/> a
/// <paramref name="WHasta"/> a lo largo de las viguetas (su tramo libre). En mm.
/// </summary>
public sealed record RegionReparto(double Desde, double Hasta, double WDesde, double WHasta);

/// <summary>
/// Una vigueta de borde de un lado libre de una zona o de un vacío (#954, decisión 4): <paramref name="Desde"/> y
/// <paramref name="Hasta"/> son su eje en planta, en mm. <paramref name="DeVacio"/>: es de un vacío (decisión 11).
/// </summary>
public sealed record ViguetaBorde(string Id, string Panel, Punto Desde, Punto Hasta, bool Borrada, bool DeVacio = false)
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
        List<List<Vigueta>> grupos = continuas ? Cadenas(Vivas) : Vivas.Select(v => new List<Vigueta> { v }).ToList();
        List<(Punto Desde, Punto Hasta)> lineas = grupos.Select(c => (Punto(c[0].Cota, c[0].EjeDesde), Punto(c[0].Cota, c[^1].EjeHasta))).ToList();
        double Cota(Punto p) => Direccion == Direccion.X ? p.Y : p.X;
        double Largo(Punto p) => Direccion == Direccion.X ? p.X : p.Y;
        foreach (ViguetaBorde b in DeBorde.Where(b => !b.Borrada))
        {
            // #954, decisión 25: la vigueta que sigue la línea de una vigueta de borde de un vacío, del otro lado de la viga,
            // se crea con ella como una sola.
            int k = !continuas || Math.Abs(Cota(b.Desde) - Cota(b.Hasta)) > 1 ? -1 : lineas.FindIndex(l => Math.Abs(Cota(l.Desde) - Cota(b.Desde)) <= 1
                && new[] { Largo(l.Desde), Largo(l.Hasta) }.Any(x => Math.Abs(x - Largo(b.Desde)) <= Union || Math.Abs(x - Largo(b.Hasta)) <= Union));
            if (k < 0)
            {
                lineas.Add((b.Desde, b.Hasta));
                continue;
            }
            double cota = Cota(lineas[k].Desde);
            double[] largos = [Largo(lineas[k].Desde), Largo(lineas[k].Hasta), Largo(b.Desde), Largo(b.Hasta)];
            lineas[k] = (Punto(cota, largos.Min()), Punto(cota, largos.Max()));
        }
        return lineas;
    }

    /// <summary>
    /// Las piezas que se crean juntas, como una sola vigueta (decisión 13 de #947): las de la misma línea que se tocan en
    /// el eje de una viga, salvo donde esa viga termina dentro del panel (tope, decisión 30). En orden a lo largo.
    /// </summary>
    public static List<List<Vigueta>> Cadenas(IEnumerable<Vigueta> piezas)
    {
        var cadenas = new List<List<Vigueta>>();
        foreach (IGrouping<double, Vigueta> fila in piezas.GroupBy(v => Math.Round(v.Cota)))
        {
            List<Vigueta>? actual = null;
            foreach (Vigueta v in fila.OrderBy(v => v.EjeDesde))
            {
                if (actual is not null && !actual[^1].TopeHasta && !v.TopeDesde && Math.Abs(actual[^1].EjeHasta - v.EjeDesde) <= Union)
                {
                    actual.Add(v);
                    continue;
                }
                actual = [v];
                cadenas.Add(actual);
            }
        }
        return cadenas;
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
        IEnumerable<(Punto Desde, Punto Hasta)>? ocupadas = null, IEnumerable<BordePanel>? divisiones = null,
        IEnumerable<Panel>? prolongarPor = null, IEnumerable<(double Cota, double Desde, double Hasta)>? lineasExistentes = null)
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
        List<Panel> todos = paneles.ToList();
        Dictionary<string, List<ViguetaBorde>> deBorde = todos.ToDictionary(p => p.Id, p => BordesDe(p, repartoEnY, borradas, vigas));
        (double Desde, double Hasta) Rango(Panel p) =>
            (p.Libre.Min(q => repartoEnY ? q.Y : q.X), p.Libre.Max(q => repartoEnY ? q.Y : q.X));

        // Decisión 18: una zona con vacíos o con vigas paralelas dentro se reparte por regiones; las demás, como antes.
        Dictionary<string, List<Banda>> bandasDe = todos.ToDictionary(p => p.Id, p => Bandas(p, paralelas, repartoEnY, deBorde[p.Id], ancho));
        bool PorRegiones(Panel p) => bandasDe[p.Id].Count > 0 || (p.HuecosLibre?.Count ?? 0) > 0;

        foreach (List<Panel> linea in Lineas(todos, Rango))
        {
            Panel primero = linea[0];
            (double gd, double gh) = Rango(primero);
            List<double> puntas = Puntas(linea, vigas, repartoEnY, ancho).Select(x => x.U).ToList();
            List<double> delPrimero = PorRegiones(primero) ? [] : Repartir(gd, gh, puntas, ejeAEje, ancho);
            foreach (Panel panel in linea)
            {
                if (PorRegiones(panel))
                {
                    (List<RepartoPanel> deRegiones, List<Vigueta> deEllas) = PorRegion(panel, bandasDe[panel.Id], repartoEnY, ejeAEje,
                        ancho, vigas, guias, borradas, deBorde[panel.Id]);
                    repartos.AddRange(deRegiones);
                    piezas.AddRange(deEllas.Where(v => !YaEsta(v, yaEstan)));
                    continue;
                }
                (double desde, double hasta) = Rango(panel);
                bool Entra(double c) => c - ancho / 2 >= desde - 1 && c + ancho / 2 <= hasta + 1;
                List<double> existentes = guias?.Where(Entra).OrderBy(c => c).ToList() ?? [];
                List<double> alineadas = panel == primero ? [] : delPrimero.Where(Entra).ToList();
                List<double> cotas;
                RepartoPanel reparto;
                if (existentes.Count > 0 || alineadas.Count > 0)
                {
                    bool siguen = existentes.Count > 0;
                    // Decisión 26: siguiendo las que ya están, lo que ellas no cubren se rellena con la fórmula.
                    cotas = siguen ? Seguir(existentes, desde, hasta, ejeAEje, ancho) : alineadas;
                    reparto = Resumir(panel.Id, desde, hasta, cotas, ejeAEje, ancho,
                        siguen ? OrigenReparto.Existentes : OrigenReparto.Alineado, siguen ? null : primero.Id);
                }
                else if (panel == primero && delPrimero.Count > 0 && puntas.Any(Entra))
                {
                    // Reparto parejo con una vigueta en la punta (decisión 31).
                    cotas = delPrimero;
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
                    piezas.AddRange(Piezas(panel, cotas[i], $"{panel.Id}:{i + 1}", null, repartoEnY, vigas, borradas, ancho).Where(v => !YaEsta(v, yaEstan)));
            }
        }
        if (repartos.Count == 0)
            return null;
        if (prolongarPor is not null)
            piezas.AddRange(Prolongar(piezas, prolongarPor.ToList(), lineasExistentes?.ToList() ?? [], repartoEnY, ejeAEje, vigas, borradas, ancho));
        List<ViguetaBorde> bordes = todos.SelectMany(p => deBorde[p.Id]).ToList();
        (List<Vigueta> conTipo, List<TipoVigueta> tipos) = Tipificar(piezas);
        return new PropuestaViguetas(direccion, ancho, repartos, conTipo, tipos, bordes);
    }

    /// <summary>
    /// Decisión 28: cada vigueta propuesta sigue por su línea, de los dos lados, por los paneles de
    /// <paramref name="resto"/> que la continúan a través de una viga. Para donde ya hay una vigueta en esa línea o donde
    /// quedaría a menos de medio eje a eje de otra (<paramref name="existentes"/>), o si la pieza se eliminó.
    /// </summary>
    private static List<Vigueta> Prolongar(List<Vigueta> piezas, List<Panel> resto, List<(double Cota, double Desde, double Hasta)> existentes,
        bool repartoEnY, double ejeAEje, List<BordePanel> vigas, IReadOnlySet<string> borradas, double ancho)
    {
        var nuevas = new List<Vigueta>();
        bool Libre(double cota, double d, double h) => !existentes.Any(o =>
            Math.Min(o.Hasta, h) - Math.Max(o.Desde, d) > PiezaMinima && Math.Abs(o.Cota - cota) < ejeAEje / 2);
        foreach (List<Vigueta> cadena in PropuestaViguetas.Cadenas(piezas.Where(v => !v.Borrada)))
        {
            double cota = cadena[0].Cota;
            var usados = new HashSet<string>();
            void Avanzar(double extremo, bool haciaArriba)
            {
                while (true)
                {
                    Panel? siguiente = null;
                    (double D, double H) tramo = default;
                    foreach (Panel q in resto.Where(q => !usados.Contains(q.Id)))
                        foreach ((double d, double h) in Geometria.CortesAnillos(q.AnillosEje, repartoEnY, cota))
                            if (Math.Abs((haciaArriba ? d : h) - extremo) <= MismaLinea)
                                (siguiente, tramo) = (q, (d, h));
                    if (siguiente is null || !Libre(cota, tramo.D, tramo.H))
                        return;
                    usados.Add(siguiente.Id);
                    string id = $"{siguiente.Id}:L{Math.Round(cota)}";
                    List<Vigueta> deEste = Piezas(siguiente, cota, id, null, repartoEnY, vigas, borradas, ancho).ToList();
                    nuevas.AddRange(deEste);
                    if (deEste.Count == 0 || deEste.Any(v => v.Borrada))
                        return;
                    extremo = haciaArriba ? tramo.H : tramo.D;
                }
            }
            Avanzar(cadena[^1].EjeHasta, haciaArriba: true);
            Avanzar(cadena[0].EjeDesde, haciaArriba: false);
        }
        return nuevas;
    }

    /// <summary>
    /// Decisión 19: los tipos van por vigueta creada (las piezas que se crean juntas, <see cref="PropuestaViguetas.Cadenas"/>).
    /// Dos son del mismo tipo si empiezan y terminan en el mismo lugar a lo largo (al cm): mismo largo y mismas vigas de
    /// apoyo. Se numeran del más largo al más corto; el largo es el libre de punta a punta. Las eliminadas no tienen tipo.
    /// </summary>
    private static (List<Vigueta> Piezas, List<TipoVigueta> Tipos) Tipificar(List<Vigueta> piezas)
    {
        var clave = new Dictionary<Vigueta, (double Largo, double Desde, double Hasta)>();
        foreach (List<Vigueta> c in PropuestaViguetas.Cadenas(piezas.Where(v => !v.Borrada)))
        {
            var k = (Math.Round((c[^1].Hasta - c[0].Desde) / 10), Math.Round(c[0].EjeDesde / 10), Math.Round(c[^1].EjeHasta / 10));
            foreach (Vigueta v in c)
                clave[v] = k;
        }
        List<IGrouping<(double Largo, double Desde, double Hasta), Vigueta>> grupos = clave.GroupBy(x => x.Value, x => x.Key)
            .OrderByDescending(g => g.Key.Largo).ThenBy(g => g.Key.Desde).ThenBy(g => g.Key.Hasta).ToList();
        var numero = grupos.Select((g, i) => (g.Key, i + 1)).ToDictionary(x => x.Key, x => x.Item2);
        // Cantidad: viguetas creadas (cadenas), no piezas.
        List<TipoVigueta> tipos = grupos.Select((g, i) => new TipoVigueta(i + 1, g.Key.Largo * 10,
            PropuestaViguetas.Cadenas(g).Count)).ToList();
        return (piezas.Select(v => clave.TryGetValue(v, out var k) ? v with { Tipo = numero[k] } : v).ToList(), tipos);
    }

    /// <summary>
    /// Decisión 18: el reparto de una zona por regiones (<see cref="Regiones"/>). Cada región sigue las viguetas que ya
    /// están en ella o, si no hay, la fórmula (con la vigueta en la punta de una viga suelta, decisión 31); sus viguetas
    /// solo van por su tramo. Decisión 25: las regiones que se tocan a través de una viga van juntas; manda la que tiene
    /// algo particular y las demás la siguen (<see cref="Seguir"/>). Ids por región y cota, de abajo arriba y de
    /// izquierda a derecha.
    /// </summary>
    private static (List<RepartoPanel> Repartos, List<Vigueta> Piezas) PorRegion(Panel panel, List<Banda> bandas, bool repartoEnY,
        double ejeAEje, double ancho, List<BordePanel> vigas, IReadOnlyList<double>? guias, IReadOnlySet<string> borradas,
        List<ViguetaBorde> bordes)
    {
        var repartos = new List<RepartoPanel>();
        var cotas = new List<(RegionReparto Region, double Cota)>();
        List<(double U, double W)> puntas = Puntas([panel], vigas, repartoEnY, ancho);
        // Las vigas que cruzan las viguetas sin terminar sueltas adentro parten la zona en luces; la viga corta no (va con
        // su tope y su vigueta en la punta, decisiones 30 y 31).
        List<BordePanel> cortan = vigas.Where(v => !Paralela(v, repartoEnY)
            && !(Adentro(panel, v.Desde) && Suelta(v, v.Desde, vigas)) && !(Adentro(panel, v.Hasta) && Suelta(v, v.Hasta, vigas))).ToList();
        List<RegionReparto> regiones = Regiones(panel, repartoEnY, bandas, cortan);
        var propias = new List<double>[regiones.Count];
        var manda = new bool[regiones.Count];
        var existe = new bool[regiones.Count];
        for (int i = 0; i < regiones.Count; i++)
        {
            RegionReparto r = regiones[i];
            bool Entra(double c) => c - ancho / 2 >= r.Desde - 1 && c + ancho / 2 <= r.Hasta + 1;
            List<double> existentes = guias?.Where(Entra).OrderBy(c => c).ToList() ?? [];
            existe[i] = existentes.Count > 0;
            (propias[i], bool conPunta) = existe[i]
                ? (Seguir(existentes, r.Desde, r.Hasta, ejeAEje, ancho), false)
                : RepartirConPunta(r.Desde, r.Hasta, puntas.Where(x => x.W >= r.WDesde - 1 && x.W <= r.WHasta + 1).Select(x => x.U).ToList(), ejeAEje, ancho);
            manda[i] = existe[i] || conPunta;
        }

        // Decisión 25: vecinas a través de una viga, en grupos; en cada grupo, la más angosta (contenida en otra) también manda.
        List<List<int>> grupos = Grupos(regiones, cortan, repartoEnY, ancho);
        bool Contenida(RegionReparto a, RegionReparto b) => a.Desde >= b.Desde - 1 && a.Hasta <= b.Hasta + 1 && b.Hasta - b.Desde - (a.Hasta - a.Desde) > 1;
        var siguiendo = new bool[regiones.Count];
        foreach (List<int> g in grupos.Where(g => g.Count > 1))
        {
            List<int> mandan = g.Where(i => manda[i] || g.Any(j => j != i && Contenida(regiones[i], regiones[j]))).ToList();
            // Las líneas de las viguetas de borde de un vacío que llegan a cualquiera del grupo valen para todo el grupo:
            // así toda la columna sigue la misma línea, viga tras viga.
            List<double> deBordes = g.SelectMany(i => BordesQueLlegan(regiones[i], bordes, repartoEnY)).ToList();
            foreach (int i in g.Except(mandan))
            {
                RegionReparto r = regiones[i];
                List<double> guia = mandan.Where(j => Math.Min(regiones[j].Hasta, r.Hasta) - Math.Max(regiones[j].Desde, r.Desde) > ancho)
                    .SelectMany(j => propias[j]).Concat(deBordes)
                    .Where(c => c - ancho / 2 >= r.Desde - 1 && c + ancho / 2 <= r.Hasta + 1)
                    .Order().ToList();
                // La misma línea puede venir de dos que mandan (arriba y abajo de un vacío): una sola vez.
                guia = guia.Where((c, k) => k == 0 || c - guia[k - 1] > 1).ToList();
                if (guia.Count == 0)
                    continue;
                propias[i] = Seguir(guia, r.Desde, r.Hasta, ejeAEje, ancho);
                siguiendo[i] = true;
            }
        }

        for (int i = 0; i < regiones.Count; i++)
        {
            if (propias[i].Count == 0)
                continue;
            RegionReparto r = regiones[i];
            repartos.Add(Resumir(panel.Id, r.Desde, r.Hasta, propias[i], ejeAEje, ancho,
                existe[i] ? OrigenReparto.Existentes : siguiendo[i] ? OrigenReparto.Alineado : OrigenReparto.Excel,
                siguiendo[i] ? panel.Id : null) with { Region = r });
            cotas.AddRange(propias[i].Select(c => (r, c)));
        }
        var piezas = new List<Vigueta>();
        int numero = 0;
        foreach ((RegionReparto r, double c) in cotas.OrderBy(x => x.Cota).ThenBy(x => x.Region.WDesde))
            piezas.AddRange(Piezas(panel, c, $"{panel.Id}:{++numero}", (r.WDesde, r.WHasta), repartoEnY, vigas, borradas, ancho));
        return (repartos, piezas);
    }

    /// <summary>
    /// Decisión 18: las regiones de una zona. Se corta en franjas (en la dirección del reparto) por cada vértice de su
    /// libre y de sus vacíos y por cada cara de una banda; en cada franja, los tramos libres a lo largo, sin los vacíos y
    /// sin las bandas donde están, y partidos por las vigas que cruzan las viguetas de lado a lado (<paramref name="cortan"/>,
    /// de cara a cara): cada panel entre vigas es su propia luz (decisión 22). Las franjas vecinas con el mismo tramo son
    /// la misma región: ahí cada vigueta va entre los mismos apoyos.
    /// </summary>
    private static List<RegionReparto> Regiones(Panel panel, bool repartoEnY, List<Banda> bandas, List<BordePanel> cortan)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        var cortes = new List<double>();
        foreach (double u in panel.AnillosLibre.SelectMany(r => r.Select(U)).Concat(bandas.SelectMany(b => new[] { b.U0, b.U1 })).Order())
            if (cortes.Count == 0 || u - cortes[^1] > 1)
                cortes.Add(u);
        var regiones = new List<RegionReparto>();
        var abiertas = new List<RegionReparto>();
        for (int i = 0; i + 1 < cortes.Count; i++)
        {
            (double a, double b) = (cortes[i], cortes[i + 1]);
            double m = (a + b) / 2;
            var tramos = Geometria.CortesAnillos(panel.AnillosLibre, repartoEnY, m);
            // Primero las vigas que cruzan (de cara a cara) y después las bandas, que llegan hasta el eje de esas vigas.
            foreach (BordePanel v in cortan)
                if (CruceEnLinea(v, m, repartoEnY) is double w)
                {
                    double medio = MedioALoLargo(v, repartoEnY);
                    tramos = tramos.SelectMany(t => t.Desde + medio < w && w < t.Hasta - medio ? Restar(t, (w - medio, w + medio)) : [t]).ToList();
                }
            foreach (Banda bd in bandas.Where(bd => bd.U0 < m && m < bd.U1))
                tramos = tramos.SelectMany(t => Restar(t, (bd.W0, bd.W1))).ToList();
            var siguen = new List<RegionReparto>();
            foreach ((double d, double h) in tramos.Where(t => t.Hasta - t.Desde >= PiezaMinima))
            {
                RegionReparto? previa = abiertas.FirstOrDefault(r => Math.Abs(r.WDesde - d) <= 1 && Math.Abs(r.WHasta - h) <= 1 && Math.Abs(r.Hasta - a) <= 1);
                if (previa is not null)
                {
                    abiertas.Remove(previa);
                    regiones.Remove(previa);
                }
                RegionReparto nueva = previa is null ? new RegionReparto(a, b, d, h) : previa with { Hasta = b };
                regiones.Add(nueva);
                siguen.Add(nueva);
            }
            abiertas = siguen;
        }
        return regiones;
    }

    /// <summary>
    /// Decisión 25: las regiones en grupos de vecinas: dos son vecinas si se tocan a través de una viga de
    /// <paramref name="cortan"/> (una termina en una cara y la otra empieza en la otra) y se superponen en la dirección
    /// del reparto. Lo de arriba y lo de abajo de un vacío no son vecinas: ahí se desligan.
    /// </summary>
    private static List<List<int>> Grupos(List<RegionReparto> regiones, List<BordePanel> cortan, bool repartoEnY, double ancho)
    {
        int n = regiones.Count;
        int[] padre = Enumerable.Range(0, n).ToArray();
        int Raiz(int i) => padre[i] == i ? i : padre[i] = Raiz(padre[i]);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                RegionReparto a = regiones[i], b = regiones[j];
                double u0 = Math.Max(a.Desde, b.Desde), u1 = Math.Min(a.Hasta, b.Hasta);
                if (i == j || u1 - u0 <= ancho)
                    continue;
                double m = (u0 + u1) / 2;
                bool vecinas = cortan.Any(v => CruceEnLinea(v, m, repartoEnY) is double w && MedioALoLargo(v, repartoEnY) is double medio
                    && Math.Abs(a.WHasta - (w - medio)) <= 2 && Math.Abs(b.WDesde - (w + medio)) <= 2);
                if (vecinas)
                    padre[Raiz(i)] = Raiz(j);
            }
        return Enumerable.Range(0, n).GroupBy(Raiz).Select(g => g.ToList()).ToList();
    }

    /// <summary>
    /// Decisión 25: las líneas de las viguetas de borde de un vacío (en el sentido de las viguetas, no eliminadas) que
    /// llegan, por la viga, al otro lado de <paramref name="r"/>: la región que sigue continúa esa misma línea.
    /// </summary>
    private static IEnumerable<double> BordesQueLlegan(RegionReparto r, List<ViguetaBorde> bordes, bool repartoEnY)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        double W(Punto p) => repartoEnY ? p.X : p.Y;
        foreach (ViguetaBorde b in bordes.Where(b => b.DeVacio && !b.Borrada && Math.Abs(U(b.Hasta) - U(b.Desde)) < 1))
        {
            (double w0, double w1) = (Math.Min(W(b.Desde), W(b.Hasta)), Math.Max(W(b.Desde), W(b.Hasta)));
            // Termina en el eje de la viga de un lado de la región (entre la cara y medio ancho de viga grande más allá).
            if (r.WDesde - w1 is >= 0 and <= ViaAncha || w0 - r.WHasta is >= 0 and <= ViaAncha)
                yield return U(b.Desde);
        }
    }

    /// <summary>Medio ancho (mm) de la viga más ancha que se espera entre una vigueta de borde y la región del otro lado.</summary>
    private const double ViaAncha = 400;

    /// <summary>
    /// Decisión 25: las líneas de una región que sigue a otra: las de la <paramref name="guia"/>; entre dos líneas muy
    /// separadas, y entre la primera o la última y el borde de la región, la fórmula centrada en ese espacio (así ninguna
    /// queda pegada a la viga).
    /// </summary>
    private static List<double> Seguir(List<double> guia, double desde, double hasta, double ejeAEje, double ancho)
    {
        double maximo = ejeAEje - ancho;
        var cotas = new List<double>(guia);
        void Rellenar(double d, double h)
        {
            if (h - d > maximo + 1 && RepartoViguetas.Calcular(h - d, ejeAEje, ancho) is Reparto r)
                cotas.AddRange(Cotas(r, d, ancho));
        }
        for (int i = 0; i + 1 < guia.Count; i++)
            Rellenar(guia[i] + ancho / 2, guia[i + 1] - ancho / 2);
        Rellenar(desde, guia[0] - ancho / 2);
        Rellenar(guia[^1] + ancho / 2, hasta);
        return cotas.Order().ToList();
    }

    /// <summary>Medio ancho de la viga medido a lo largo de las viguetas: más largo si la viga las cruza en diagonal.</summary>
    private static double MedioALoLargo(BordePanel v, bool repartoEnY)
    {
        double du = Math.Abs(repartoEnY ? v.Hasta.Y - v.Desde.Y : v.Hasta.X - v.Desde.X);
        return v.Ancho / 2 / (du / Geometria.Distancia(v.Desde, v.Hasta));
    }

    /// <summary>El tramo <paramref name="t"/> sin la parte <paramref name="menos"/>: cero, uno o dos tramos.</summary>
    private static IEnumerable<(double Desde, double Hasta)> Restar((double Desde, double Hasta) t, (double Desde, double Hasta) menos)
    {
        if (menos.Hasta <= t.Desde || menos.Desde >= t.Hasta)
        {
            yield return t;
            yield break;
        }
        if (menos.Desde > t.Desde)
            yield return (t.Desde, menos.Desde);
        if (menos.Hasta < t.Hasta)
            yield return (menos.Hasta, t.Hasta);
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
    /// fórmula del Excel. Con puntas, el reparto parejo en el que una vigueta cae en una de ellas (<see cref="Parejo"/>,
    /// probando en orden) o, si no lo hay, la fórmula sola: siempre pareja y centrada (#954, decisión 23; antes se sumaba
    /// aparte la vigueta de la punta y los extremos quedaban distintos).
    /// </summary>
    private static List<double> Repartir(double desde, double hasta, List<double> puntas, double ejeAEje, double ancho) =>
        RepartirConPunta(desde, hasta, puntas, ejeAEje, ancho).Cotas;

    /// <summary>Como <see cref="Repartir"/>, y si salió con la vigueta en la punta de una viga (para la decisión 25).</summary>
    private static (List<double> Cotas, bool ConPunta) RepartirConPunta(double desde, double hasta, List<double> puntas, double ejeAEje, double ancho)
    {
        if (hasta - desde < ancho)
            return ([], false);
        Reparto? formula = RepartoViguetas.Calcular(hasta - desde, ejeAEje, ancho);
        if (formula is null)
            return ([], false);
        foreach (double c in puntas.Where(c => c - ancho / 2 >= desde - 1 && c + ancho / 2 <= hasta + 1))
            if (Parejo(desde, hasta, c, formula.Cantidad, ejeAEje, ancho) is { } parejo)
                return (parejo, true);
        return (Cotas(formula, desde, ancho), false);
    }

    /// <summary>
    /// Cuántas viguetas más que la fórmula se aceptan para que el reparto parejo caiga en la punta (#954, decisión 17: con
    /// más, la separación se achicaba hasta 0,23 m).
    /// </summary>
    private const int MasViguetas = 1;

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
                // Decisión 23: no más apretado que la mitad del eje a eje (ni que dos anchos).
                if (s >= Math.Max(2 * ancho, ejeAEje / 2) && s <= ejeAEje + 1e-6 && extremo >= -1e-6 && extremo <= maximo + 1e-6 && (mejor is null || s > mejor))
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
    /// al ras de la punta. Las vigas paralelas a las viguetas no cuentan: no las cruzan. Solo las puntas sueltas (#954,
    /// decisión 16). <c>W</c>: dónde está la punta a lo largo de las viguetas.
    /// </summary>
    private static List<(double U, double W)> Puntas(List<Panel> linea, List<BordePanel> vigas, bool repartoEnY, double ancho)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        double W(Punto p) => repartoEnY ? p.X : p.Y;
        return vigas
            .Where(v => Math.Abs(U(v.Hasta) - U(v.Desde)) > 0.02 * Geometria.Distancia(v.Desde, v.Hasta))
            .SelectMany(v => new[] { (Viga: v, Punta: v.Desde, Otra: v.Hasta), (Viga: v, Punta: v.Hasta, Otra: v.Desde) })
            .Where(x => linea.Any(panel => Adentro(panel, x.Punta)) && Suelta(x.Viga, x.Punta, vigas))
            .Select(x => (U(x.Punta) + Math.Sign(U(x.Otra) - U(x.Punta)) * ancho / 2, W(x.Punta)))
            .OrderBy(x => x.Item1).ToList();
    }

    /// <summary>A esto (mm) o menos del cuerpo de otro elemento, la punta de una viga se apoya en él.</summary>
    private const double Apoyada = 30;

    /// <summary>
    /// Si la punta <paramref name="p"/> de <paramref name="viga"/> queda suelta (#954, decisión 16): no se apoya en otro
    /// elemento del armazón (a medio ancho + 3 cm de su eje, o menos, entre sus puntas). Las vigas de cada vano que llegan
    /// a la viga del medio de una zona no quedan sueltas.
    /// </summary>
    private static bool Suelta(BordePanel viga, Punto p, List<BordePanel> vigas) =>
        !vigas.Any(o => !ReferenceEquals(o, viga) && o.Id != viga.Id && DistanciaASegmento(p, o.Desde, o.Hasta) <= o.Ancho / 2 + Apoyada);

    private static double DistanciaASegmento(Punto p, Punto a, Punto b) => Geometria.DistanciaAlBorde([a, b], p);

    /// <summary>Una viga es paralela a las viguetas si casi no avanza en la dirección del reparto.</summary>
    private static bool Paralela(BordePanel v, bool repartoEnY)
    {
        double du = repartoEnY ? v.Hasta.Y - v.Desde.Y : v.Hasta.X - v.Desde.X;
        return Math.Abs(du) <= 0.02 * Geometria.Distancia(v.Desde, v.Hasta);
    }

    /// <summary>
    /// Las viguetas de borde del panel (ids <c>P:B1</c>…, primero las del contorno). Las de un vacío que van en el sentido
    /// de las viguetas se alargan por su línea, de los dos lados, hasta el eje de la primera viga de <paramref name="vigas"/>
    /// que la cruza, o hasta el apoyo del contorno o otro vacío si llegan antes (decisiones 11 y 22).
    /// </summary>
    private static List<ViguetaBorde> BordesDe(Panel panel, bool repartoEnY, IReadOnlySet<string> borradas, List<BordePanel> vigas)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        double W(Punto p) => repartoEnY ? p.X : p.Y;
        Punto En(double cota, double largo) => repartoEnY ? new Punto(largo, cota) : new Punto(cota, largo);
        IReadOnlyList<IReadOnlyList<Punto>> anillos = [panel.AnillosApoyo[0], .. panel.HuecosEje ?? []];
        return panel.ViguetasDeBorde.Select((b, i) =>
        {
            string id = $"{panel.Id}:B{i + 1}";
            (Punto desde, Punto hasta) = b;
            bool deVacio = i >= panel.BordesDelContorno;
            if (deVacio && Math.Abs(U(hasta) - U(desde)) <= 0.02 * Geometria.Distancia(desde, hasta))
            {
                double cota = (U(desde) + U(hasta)) / 2, w0 = Math.Min(W(desde), W(hasta)), w1 = Math.Max(W(desde), W(hasta));
                double medio = (w0 + w1) / 2;
                // Decisión 22: donde una viga cruza la línea, ahí para.
                List<double> cruces = vigas.Select(v => CruceEnLinea(v, cota, repartoEnY)).OfType<double>().ToList();
                foreach ((double d, double h) in Geometria.CortesAnillos(anillos, repartoEnY, cota))
                    if (d <= medio && medio <= h)
                    {
                        // También para en una viga que va justo en la punta del vacío (su lado va sobre ella).
                        double ini = cruces.Where(w => w <= w0 + 1 && w > d).DefaultIfEmpty(d).Max();
                        double fin = cruces.Where(w => w >= w1 - 1 && w < h).DefaultIfEmpty(h).Min();
                        (desde, hasta) = W(desde) <= W(hasta) ? (En(cota, ini), En(cota, fin)) : (En(cota, fin), En(cota, ini));
                    }
            }
            return new ViguetaBorde(id, panel.Id, desde, hasta, borradas.Contains(id), deVacio);
        }).ToList();
    }

    /// <summary>Dónde (a lo largo) cruza el eje de la viga la línea de cota <paramref name="cota"/>; null si no la cruza.</summary>
    private static double? CruceEnLinea(BordePanel v, double cota, bool repartoEnY)
    {
        (double ua, double ub) = repartoEnY ? (v.Desde.Y, v.Hasta.Y) : (v.Desde.X, v.Hasta.X);
        (double wa, double wb) = repartoEnY ? (v.Desde.X, v.Hasta.X) : (v.Desde.Y, v.Hasta.Y);
        if ((ua - cota) * (ub - cota) > 0 || Math.Abs(ub - ua) < 1)
            return null;
        return wa + (cota - ua) / (ub - ua) * (wb - wa);
    }

    /// <summary>
    /// Un cuerpo que ninguna vigueta puede pisar dentro de una zona: de <paramref name="U0"/> a <paramref name="U1"/> en la
    /// dirección del reparto (de cara a cara) y de <paramref name="W0"/> a <paramref name="W1"/> a lo largo.
    /// </summary>
    private sealed record Banda(double U0, double U1, double W0, double W1);

    /// <summary>
    /// Los cuerpos de las vigas paralelas que quedan dentro del ancho libre del panel y corren a lo largo de él al menos
    /// una pieza mínima, por dentro del panel (decisión 10), y de las viguetas de borde de los vacíos que van en el sentido
    /// de las viguetas y no se eliminaron (decisión 11), cada uno solo donde está a lo largo (decisión 18).
    /// </summary>
    private static List<Banda> Bandas(Panel panel, List<BordePanel> paralelas, bool repartoEnY, List<ViguetaBorde> bordes, double ancho)
    {
        double U(Punto p) => repartoEnY ? p.Y : p.X;
        double W(Punto p) => repartoEnY ? p.X : p.Y;
        (double desde, double hasta) = (panel.Libre.Min(U), panel.Libre.Max(U));
        (double l0, double l1) = (panel.Libre.Min(W), panel.Libre.Max(W));
        var bandas = new List<Banda>();
        foreach (BordePanel v in paralelas)
        {
            double eje = (U(v.Desde) + U(v.Hasta)) / 2, medio = v.Ancho / 2;
            double w0 = Math.Max(l0, Math.Min(W(v.Desde), W(v.Hasta))), w1 = Math.Min(l1, Math.Max(W(v.Desde), W(v.Hasta)));
            if (eje + medio <= desde + 1 || eje - medio >= hasta - 1 || w1 - w0 < PiezaMinima)
                continue;
            Punto centro = repartoEnY ? new Punto((w0 + w1) / 2, eje) : new Punto(eje, (w0 + w1) / 2);
            if (Geometria.Dentro(panel.Eje, centro))
                bandas.Add(new Banda(eje - medio, eje + medio, w0, w1));
        }
        foreach (ViguetaBorde b in bordes.Where(b => b.DeVacio && !b.Borrada && Math.Abs(U(b.Hasta) - U(b.Desde)) < 1))
        {
            double eje = U(b.Desde);
            if (eje > desde && eje < hasta)
                bandas.Add(new Banda(eje - ancho / 2, eje + ancho / 2, Math.Min(W(b.Desde), W(b.Hasta)), Math.Max(W(b.Desde), W(b.Hasta))));
        }
        return bandas.OrderBy(b => b.U0).ToList();
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

    /// <summary>
    /// Las piezas de la vigueta <paramref name="numero"/> del panel (su id, <c>P4:3</c>), a su <paramref name="cota"/>.
    /// Con <paramref name="region"/> (decisión 18), solo las piezas (entre vigas) que caen en esa región, a lo largo.
    /// </summary>
    private static IEnumerable<Vigueta> Piezas(Panel panel, double cota, string numero, (double Desde, double Hasta)? region,
        bool repartoEnY, List<BordePanel> vigas, IReadOnlySet<string> borradas, double ancho)
    {
        List<(double Desde, double Hasta)> ejes = Geometria.CortesAnillos(panel.AnillosApoyo, repartoEnY, cota);
        List<(double Desde, double Hasta)> tramos = Geometria.CortesAnillos(panel.AnillosLibre, repartoEnY, cota);
        if (region is (double r0, double r1))
            tramos = tramos.Where(t => Math.Min(t.Hasta, r1) - Math.Max(t.Desde, r0) > 1).ToList();
        for (int k = 0; k < tramos.Count; k++)
        {
            (double d, double h) = tramos[k];
            double medio = (d + h) / 2;
            (double ed, double eh) = ejes.FirstOrDefault(e => e.Desde <= medio && medio <= e.Hasta, (Desde: d, Hasta: h));
            string id = tramos.Count == 1 ? numero : $"{numero}.{k + 1}";
            var partes = Partir(panel, d, h, ed, eh, cota, repartoEnY, vigas, ancho);
            if (region is (double rd, double rh))
                partes = partes.Where(x => (x.D + x.H) / 2 >= rd - 1 && (x.D + x.H) / 2 <= rh + 1).ToList();
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
    /// Si la punta <paramref name="p"/> de una viga queda dentro del panel y no en su borde (decisión 30): tampoco en uno
    /// de sus vacíos ni en el borde de uno (#954, decisión 16).
    /// </summary>
    private static bool Adentro(Panel panel, Punto p) =>
        Geometria.Dentro(panel.Eje, p) && !(panel.HuecosEje ?? []).Any(h => Geometria.Dentro(h, p))
        && panel.AnillosEje.All(r => Geometria.DistanciaAlBorde(r, p) > PuntaAdentro);

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
                cortes.Add((w, medio, !enPunta && (Adentro(panel, v.Desde) && Suelta(v, v.Desde, vigas)
                    || Adentro(panel, v.Hasta) && Suelta(v, v.Hasta, vigas))));
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
