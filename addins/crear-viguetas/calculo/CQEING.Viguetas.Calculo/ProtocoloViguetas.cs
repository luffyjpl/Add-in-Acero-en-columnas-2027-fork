using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CQEING.Viguetas.Calculo;

/// <summary>Un mensaje de la página de «Crear viguetas» (#951).</summary>
public abstract record MensajeViguetas;

public sealed record ListoViguetas : MensajeViguetas;

public sealed record ElegirPiso(string Id, bool ViguetasComoBorde) : MensajeViguetas;

/// <summary>
/// Pide la propuesta de uno o varios paneles (decisión 12). Medidas en mm; <paramref name="Borradas"/> son ids de
/// <see cref="Vigueta"/>; <paramref name="Continuas"/>: crear de punta a punta a través de las vigas (decisión 13);
/// <paramref name="Seguir"/>: seguir las líneas de las viguetas que ya están (decisión 16); <paramref name="QuitarModelo"/>:
/// ids de las piezas de viguetas del modelo que se quitan al crear (<see cref="PiezaModelo"/>, decisión 22).
/// </summary>
public sealed record PedirPropuesta(IReadOnlyList<string> Paneles, Direccion Direccion, double EjeAEje, double Ancho, IReadOnlyList<string> Borradas,
    bool Continuas = true, bool Seguir = true, IReadOnlyList<string>? QuitarModelo = null) : MensajeViguetas
{
    public IReadOnlyList<string> DelModelo => QuitarModelo ?? [];
}

public sealed record CrearViguetas(PedirPropuesta Propuesta, string Tipo) : MensajeViguetas;

public sealed record QuitarCreadas(IReadOnlyList<string> Paneles) : MensajeViguetas;

/// <summary>Arma el perímetro con estos elementos de armazón (decisión 24).</summary>
public sealed record ArmarPerimetro(IReadOnlyList<string> Ids) : MensajeViguetas;

/// <summary>
/// Arma todas las zonas por puntos de la página (#954, decisión 6): siempre llegan todas juntas y reemplazan a las
/// anteriores. <paramref name="Ancho"/> es el de vigueta, en mm: el de las viguetas de borde de los lados libres.
/// </summary>
public sealed record ArmarZonas(IReadOnlyList<ZonaPuntos> Zonas, double Ancho) : MensajeViguetas;

public sealed record CerrarViguetas : MensajeViguetas;

public sealed record DesconocidoViguetas : MensajeViguetas;

/// <summary>
/// Lee y escribe los mensajes de «Crear viguetas». JSON en camelCase con los enums como texto, como el tablero de
/// vigas; los textos van sin escapar los acentos (viajan por <c>PostWebMessageAsJson</c>). Los puntos viajan como
/// [x, y] en mm.
/// </summary>
public static class ProtocoloViguetas
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Nunca lanza: un JSON roto da <see cref="DesconocidoViguetas"/>.</summary>
    public static MensajeViguetas Leer(string json)
    {
        Entrada? e;
        try
        {
            e = JsonSerializer.Deserialize<Entrada>(json, Json);
        }
        catch (JsonException)
        {
            return new DesconocidoViguetas();
        }
        if (e is null)
            return new DesconocidoViguetas();
        List<string> paneles = e.Paneles ?? [];
        PedirPropuesta Pedido() => new(paneles, e.Direccion ?? Direccion.X, e.EjeAEje ?? 0, e.Ancho ?? 0, e.Borradas ?? [], e.Continuas ?? true, e.Seguir ?? true,
            e.QuitarModelo ?? []);
        return e.Tipo switch
        {
            "listo" => new ListoViguetas(),
            "piso" when e.Id is not null => new ElegirPiso(e.Id, e.ViguetasComoBorde ?? false),
            "propuesta" when paneles.Count > 0 => Pedido(),
            "crear" when paneles.Count > 0 && e.TipoVigueta is not null => new CrearViguetas(Pedido(), e.TipoVigueta),
            "quitar" when paneles.Count > 0 => new QuitarCreadas(paneles),
            "perimetro" => new ArmarPerimetro(e.Ids ?? []),
            "zonas" => new ArmarZonas((e.Zonas ?? []).Where(z => z.Id is not null)
                .Select(z => new ZonaPuntos(z.Id!, Puntos(z.Contorno), (z.Vacios ?? []).Select(Puntos).ToList())).ToList(), e.Ancho ?? 0),
            "cerrar" => new CerrarViguetas(),
            _ => new DesconocidoViguetas(),
        };
    }

    /// <summary>Los [x, y] que mandó la página; los que no traen dos números finitos se saltan.</summary>
    private static List<Punto> Puntos(List<double[]>? xy) =>
        (xy ?? []).Where(p => p is { Length: >= 2 } && double.IsFinite(p[0]) && double.IsFinite(p[1])).Select(p => new Punto(p[0], p[1])).ToList();

    public static string Pisos(string documento, IEnumerable<PisoResumen> pisos, IEnumerable<TipoArmazon> tipos) =>
        Escribir(new { tipo = "pisos", documento, pisos, tipos });

    public static string Piso(PisoViguetas p) => Escribir(new
    {
        tipo = "piso",
        id = p.Piso.Id,
        nombre = p.Piso.Nombre,
        cota = p.Piso.Cota,
        viguetasComoBorde = p.ViguetasComoBorde,
        armazon = p.Piso.Armazon.Select(a => new { id = a.Id, desde = Xy(a.Desde), hasta = Xy(a.Hasta), ancho = a.Ancho, tipo = a.NombreTipo, esVigueta = a.EsVigueta, creada = a.Creada }),
        columnas = p.Piso.Columnas.Select(c => new { id = c.Id, min = Xy(c.Min), max = Xy(c.Max) }),
        paneles = p.Paneles.Select(x => new { id = x.Id, eje = x.Eje.Select(Xy), libre = x.Libre.Select(Xy), creadas = p.CreadasEn(x).Count }),
        suelos = p.Piso.LosSuelos.Select(s => new { id = s.Id, contornos = s.Contornos.Select(c => c.Select(Xy)) }),
        puntos = p.Puntos.Select(x => new { xy = Xy(x.P), tipo = x.Tipo }),
    });

    /// <summary>
    /// <paramref name="paneles"/> son los que pidió la página: la respuesta solo vale si siguen siendo esos.
    /// <paramref name="hayGuias"/>: hay viguetas existentes que se pueden seguir, para mostrar la opción.
    /// <paramref name="modelo"/>: las piezas de las viguetas del modelo dentro de los paneles, y cuáles se quitan
    /// (decisión 22).
    /// </summary>
    public static string Propuesta(PropuestaViguetas? p, IReadOnlyList<string> paneles, bool continuas, bool hayGuias = false,
        IEnumerable<(PiezaModelo Pieza, bool Quitada)>? modelo = null) => p is null
        ? Escribir(new { tipo = "propuesta", paneles, propuesta = (object?)null })
        : Escribir(new
        {
            tipo = "propuesta",
            paneles,
            propuesta = new
            {
                direccion = p.Direccion,
                ancho = p.Ancho,
                lineas = p.Lineas(continuas).Count,
                hayGuias,
                modelo = (modelo ?? []).Select(m => new { id = m.Pieza.Id, elemento = m.Pieza.Elemento, desde = Xy(m.Pieza.Inicio), hasta = Xy(m.Pieza.Fin), quitada = m.Quitada }),
                paneles = p.Paneles.Select(r => new
                {
                    id = r.Panel, libre = r.Libre, cantidad = r.Reparto.Cantidad, ejeAEje = r.Reparto.EjeAEje,
                    libreExtremos = r.Reparto.LibreExtremos, libreFinal = r.LibreOtroExtremo, libreMaximo = r.Reparto.LibreMaximo,
                    origen = r.Origen, guia = r.Guia,
                }),
                viguetas = p.Viguetas.Select(v => new
                {
                    id = v.Id, panel = v.Panel, cota = v.Cota, desde = Xy(p.Punto(v.Cota, v.Desde)), hasta = Xy(p.Punto(v.Cota, v.Hasta)),
                    ejeDesde = Xy(p.Punto(v.Cota, v.EjeDesde)), ejeHasta = Xy(p.Punto(v.Cota, v.EjeHasta)),
                    largo = v.Largo, tipo = v.Tipo, borrada = v.Borrada,
                }),
                tipos = p.Tipos,
                bordes = p.DeBorde.Select(b => new { id = b.Id, panel = b.Panel, desde = Xy(b.Desde), hasta = Xy(b.Hasta), largo = b.Largo, borrada = b.Borrada }),
            },
        });

    /// <summary>Los perímetros armados con <paramref name="ids"/>: vacío si los elementos no cierran.</summary>
    public static string Perimetro(IReadOnlyList<string> ids, IEnumerable<(Panel Panel, int Creadas)> perimetros) => Escribir(new
    {
        tipo = "perimetro",
        ids,
        paneles = perimetros.Select(x => new { id = x.Panel.Id, eje = x.Panel.Eje.Select(Xy), libre = x.Panel.Libre.Select(Xy), creadas = x.Creadas }),
    });

    /// <summary>
    /// Las zonas por puntos armadas (#954), como los perímetros, más sus vacíos (por ejes y entre caras) y sus viguetas de
    /// borde. Una zona pedida que no da un libre no viene.
    /// </summary>
    public static string Zonas(IEnumerable<(Panel Panel, int Creadas)> zonas) => Escribir(new
    {
        tipo = "zonas",
        paneles = zonas.Select(x => new
        {
            id = x.Panel.Id, eje = x.Panel.Eje.Select(Xy), libre = x.Panel.Libre.Select(Xy), creadas = x.Creadas,
            huecosEje = (x.Panel.HuecosEje ?? []).Select(h => h.Select(Xy)),
            huecosLibre = (x.Panel.HuecosLibre ?? []).Select(h => h.Select(Xy)),
            bordes = x.Panel.ViguetasDeBorde.Select(b => new { desde = Xy(b.Desde), hasta = Xy(b.Hasta) }),
        }),
    });

    public static string Aviso(string texto, bool bien = false) => Escribir(new { tipo = "aviso", texto, bien });

    private static double[] Xy(Punto p) => [Math.Round(p.X, 1), Math.Round(p.Y, 1)];

    private static string Escribir(object o) => JsonSerializer.Serialize(o, Json);

    private sealed class Entrada
    {
        public string? Tipo { get; set; }
        public string? Id { get; set; }
        public bool? ViguetasComoBorde { get; set; }
        public List<string>? Paneles { get; set; }
        public bool? Continuas { get; set; }
        public bool? Seguir { get; set; }
        public List<string>? QuitarModelo { get; set; }
        public List<string>? Ids { get; set; }
        public Direccion? Direccion { get; set; }
        public double? EjeAEje { get; set; }
        public double? Ancho { get; set; }
        public List<string>? Borradas { get; set; }
        public string? TipoVigueta { get; set; }
        public List<ZonaEntrada>? Zonas { get; set; }
    }

    private sealed class ZonaEntrada
    {
        public string? Id { get; set; }
        public List<double[]>? Contorno { get; set; }
        public List<List<double[]>>? Vacios { get; set; }
    }
}
