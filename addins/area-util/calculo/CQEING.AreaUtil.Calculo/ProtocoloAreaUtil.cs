using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Un estilo de línea de Revit: «CQ …», con color hexadecimal y grosor de 1 a 16.</summary>
public sealed record EstiloLinea(string Clave, string Nombre, string Color, int Grosor);

public static class EstilosLinea
{
    /// <summary>Los colores del dibujo de Carlos (min 7–10): lote negro, retiros naranjas, área útil roja.</summary>
    public static IReadOnlyList<EstiloLinea> PorDefecto { get; } =
    [
        new(Estilos.Lote, "CQ Lote", "#1F2933", 5),
        new(Estilos.RetiroFrontal, "CQ Retiro frontal", "#E67E22", 3),
        new(Estilos.RetiroLateral, "CQ Retiro lateral", "#F2A900", 3),
        new(Estilos.RetiroPosterior, "CQ Retiro posterior", "#B9770E", 3),
        new(Estilos.AreaUtil, "CQ Área útil", "#D0021B", 5),
    ];
}

/// <summary>Un nivel del modelo para la lista de la izquierda; la cota va en metros.</summary>
public sealed record NivelLeido(string Id, string Nombre, double Cota);

public abstract record MensajeAreaUtil;

public sealed record ListoAreaUtil : MensajeAreaUtil;

public sealed record CalcularAreaUtil(EntradaLote Entrada) : MensajeAreaUtil;

public sealed record LeerPuntosAreaUtil(string Texto) : MensajeAreaUtil;

/// <summary><paramref name="Compartidas"/>: los puntos vienen en coordenadas compartidas (decisión 8 del spec).</summary>
public sealed record GenerarAreaUtil(EntradaLote Entrada, string NivelId, bool Compartidas, IReadOnlyList<EstiloLinea> Estilos) : MensajeAreaUtil;

/// <summary>«Elegir en Revit» (decisión 18): dos clics en una planta de ese nivel.</summary>
public sealed record ElegirUbicacionAreaUtil(string NivelId) : MensajeAreaUtil;

public sealed record CerrarAreaUtil : MensajeAreaUtil;

public sealed record DesconocidoAreaUtil : MensajeAreaUtil;

/// <summary>Los mensajes entre la página del tablero y C#, como en zapatas.</summary>
public static class ProtocoloAreaUtil
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Nunca lanza: un JSON roto da <see cref="DesconocidoAreaUtil"/>.</summary>
    public static MensajeAreaUtil Leer(string json)
    {
        Entrada? e;
        try
        {
            e = JsonSerializer.Deserialize<Entrada>(json, Json);
        }
        catch (JsonException)
        {
            return new DesconocidoAreaUtil();
        }
        if (e is null)
            return new DesconocidoAreaUtil();
        return e.Tipo switch
        {
            "listo" => new ListoAreaUtil(),
            "calcular" => new CalcularAreaUtil(Lote(e)),
            "leerPuntos" => new LeerPuntosAreaUtil(e.Texto ?? ""),
            "generar" when !string.IsNullOrEmpty(e.NivelId) => new GenerarAreaUtil(Lote(e), e.NivelId,
                e.Modo == "puntos" && e.Coordenadas == "compartidas", e.Estilos is { Count: > 0 } s ? s : EstilosLinea.PorDefecto),
            "elegirUbicacion" => new ElegirUbicacionAreaUtil(e.NivelId ?? ""),
            "cerrar" => new CerrarAreaUtil(),
            _ => new DesconocidoAreaUtil(),
        };
    }

    private static EntradaLote Lote(Entrada e)
    {
        // Frente o fondo en 0 o negativos no son un lote (un negativo lo dibujaría espejado): sin puntos, el cálculo avisa.
        // El rectángulo se ubica aquí (decisión 18): la vista previa, las áreas y lo generado usan el mismo lote.
        var ubicacion = e.Ubicacion is { } u ? new Ubicacion(u.X, u.Y, u.Giro) : Ubicacion.Origen;
        IReadOnlyList<Punto> puntos = e.Modo == "puntos"
            ? (e.Puntos ?? []).Select(p => new Punto(p.X, p.Y)).ToList()
            : e.Frente > 0 && e.Fondo > 0 ? Poligono.Rectangulo(e.Frente.Value, e.Fondo.Value).Vertices.Select(ubicacion.Aplicar).ToList() : [];
        List<TipoLado>? tipos = e.Tipos?.Select(t => Enum.TryParse(t, out TipoLado tipo) ? tipo : (TipoLado?)null).ToList() is { } leidos
            && leidos.All(t => t is not null) ? leidos.Select(t => t!.Value).ToList() : null;
        RetirosJson r = e.Retiros ?? new RetirosJson();
        FichaJson f = e.Ficha ?? new FichaJson();
        return new EntradaLote(puntos, e.LadoFrente ?? 0, tipos, new Retiros(r.Frontal, r.Posterior, r.LateralIzquierdo, r.LateralDerecho),
            new Ficha(f.Imo, f.Imc, f.Pisos is double p ? (int)Math.Floor(p) : null, f.LoteMinimo, f.FrenteMinimo));
    }

    public static string Datos(string modelo, IReadOnlyList<NivelLeido> niveles, string? nivelActivo) =>
        Escribir(new { tipo = "datos", modelo, niveles, nivelActivo, estilos = EstilosLinea.PorDefecto });

    public static string Resultado(ResultadoLote r) => Escribir(new
    {
        tipo = "resultado",
        lote = r.Lote?.Vertices.Select(p => new { x = p.X, y = p.Y }),
        util = r.Util?.Vertices.Select(p => new { x = p.X, y = p.Y }),
        tipos = r.Tipos,
        trazos = r.Trazos.Select(t => new { ax = t.A.X, ay = t.A.Y, bx = t.B.X, by = t.B.Y, estilo = t.Estilo }),
        resumen = r.Resumen,
        avisos = r.Avisos,
        sePuedeGenerar = r.SePuedeGenerar,
    });

    public static string Puntos(List<PuntoLote> puntos, List<string> errores) => Escribir(new { tipo = "puntos", puntos, errores });

    public static string Generado(int lineas) => Escribir(new { tipo = "generado", lineas });

    public static string UbicacionElegida(Ubicacion u) => Escribir(new { tipo = "ubicacion", x = u.X, y = u.Y, giro = u.GiroGrados });

    public static string Aviso(string texto, bool bien = false) => Escribir(new { tipo = "aviso", texto, bien });

    private static string Escribir<T>(T valor) => JsonSerializer.Serialize(valor, Json);

    private sealed class Entrada
    {
        public string? Tipo { get; set; }
        public string? Modo { get; set; }
        public double? Frente { get; set; }
        public double? Fondo { get; set; }
        public List<PuntoJson>? Puntos { get; set; }
        public int? LadoFrente { get; set; }
        public List<string>? Tipos { get; set; }
        public RetirosJson? Retiros { get; set; }
        public FichaJson? Ficha { get; set; }
        public string? NivelId { get; set; }
        public string? Coordenadas { get; set; }
        public List<EstiloLinea>? Estilos { get; set; }
        public string? Texto { get; set; }
        public UbicacionJson? Ubicacion { get; set; }
    }

    private sealed class UbicacionJson
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Giro { get; set; }
    }

    private sealed class PuntoJson
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    private sealed class RetirosJson
    {
        public double Frontal { get; set; }
        public double Posterior { get; set; }
        public double LateralIzquierdo { get; set; }
        public double LateralDerecho { get; set; }
    }

    private sealed class FichaJson
    {
        public double? Imo { get; set; }
        public double? Imc { get; set; }
        // Número y no entero: «2.5» escrito a mano hacía fallar todo el mensaje y la página dejaba de recibir resultados.
        public double? Pisos { get; set; }
        public double? LoteMinimo { get; set; }
        public double? FrenteMinimo { get; set; }
    }
}
