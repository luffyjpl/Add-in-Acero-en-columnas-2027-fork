using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CQEING.Zapatas.Calculo;

/// <summary>Un mensaje de la página del tablero de zapatas (#252).</summary>
public abstract record MensajeZapatas;

public sealed record ListoZapatas : MensajeZapatas;

public sealed record CalcularZapatas(IReadOnlyList<string> Ids, ConfigParrillas Config) : MensajeZapatas;

/// <summary>Toma las zapatas seleccionadas en Revit o pide elegirlas.</summary>
public sealed record SeleccionarEnRevit : MensajeZapatas;

public sealed record MostrarEnRevit(IReadOnlyList<string> Ids) : MensajeZapatas;

/// <summary>Genera el acero. Forma: id de la forma elegida, o null para la automática; gancho: id del tipo para 135° y 180°.</summary>
public sealed record AplicarZapatas(IReadOnlyList<string> Ids, ConfigParrillas Config, string? FormaX, string? FormaY, string? GanchoX, string? GanchoY) : MensajeZapatas;

public sealed record CerrarZapatas : MensajeZapatas;

public sealed record DesconocidoZapatas : MensajeZapatas;

/// <summary>Una forma del proyecto para el catálogo, con sus curvas como polilíneas [x, y] en mm.</summary>
public sealed record FormaCatalogo(string Id, string Nombre, IReadOnlyList<IReadOnlyList<double[]>> Curvas, bool Parrilla);

/// <summary>Un tipo de gancho del proyecto para el catálogo; la extensión va en diámetros.</summary>
public sealed record GanchoCatalogo(string Id, string Nombre, int Grados, double Extension);

/// <summary>Un número de barra disponible en el proyecto, con los largos B del cuadro de ganchos en mm.</summary>
public sealed record BarraCatalogo(int Numero, string Nombre, double Diametro, double Doblado, double Gancho90, double Gancho135, double Gancho180);

public sealed record CatalogoZapatas(IReadOnlyList<BarraCatalogo> Barras, IReadOnlyList<FormaCatalogo> Formas, IReadOnlyList<GanchoCatalogo> Ganchos);

/// <summary>Lee y escribe los mensajes del tablero: JSON en camelCase con los enums como texto, como el tablero de vigas.</summary>
public static class ProtocoloZapatas
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Nunca lanza: un JSON roto da <see cref="DesconocidoZapatas"/>.</summary>
    public static MensajeZapatas Leer(string json)
    {
        Entrada? e;
        try
        {
            e = JsonSerializer.Deserialize<Entrada>(json, Json);
        }
        catch (JsonException)
        {
            return new DesconocidoZapatas();
        }
        if (e is null)
            return new DesconocidoZapatas();
        List<string> ids = e.Ids ?? [];
        return e.Tipo switch
        {
            "listo" => new ListoZapatas(),
            "calcular" when ids.Count > 0 && Config(e) is { } c => new CalcularZapatas(ids, c),
            "seleccionar" => new SeleccionarEnRevit(),
            "mostrar" when ids.Count > 0 => new MostrarEnRevit(ids),
            "aplicar" when ids.Count > 0 && Config(e) is { } c => new AplicarZapatas(ids, c, e.FormaX, e.FormaY, e.GanchoX, e.GanchoY),
            "cerrar" => new CerrarZapatas(),
            _ => new DesconocidoZapatas(),
        };
    }

    private static ConfigParrillas? Config(Entrada e) => e.X is null || e.Y is null ? null : new ConfigParrillas(
        new ConfigDireccion(e.X.Numero, e.X.Separacion, e.X.Terminacion), new ConfigDireccion(e.Y.Numero, e.Y.Separacion, e.Y.Terminacion),
        new Recubrimientos(e.Inferior ?? ValoresPorDefecto.RecubrimientoMm, e.Lateral ?? ValoresPorDefecto.RecubrimientoMm, e.Superior ?? ValoresPorDefecto.RecubrimientoMm),
        e.IncluirSuperior ?? true);

    /// <param name="existentes">Por zapata, cuántas barras tiene y cuántas son del add-in.</param>
    public static string Datos(string modelo, IReadOnlyList<ZapataLeida> zapatas, IReadOnlyList<IReadOnlyList<string>> grupos, CatalogoZapatas catalogo,
        IReadOnlyDictionary<string, (int Total, int Propias)> existentes) =>
        Escribir(new
        {
            tipo = "datos",
            modelo,
            grupos,
            catalogo,
            recubrimientoPorDefecto = ValoresPorDefecto.RecubrimientoMm,
            zapatas = zapatas.Select(z =>
            {
                existentes.TryGetValue(z.Id, out (int Total, int Propias) e);
                (double ancho, double alto) = Geometria.Caja(z.Contorno);
                return new
                {
                    z.Id, z.Nombre, z.Eje, z.Nivel, z.ZBase, z.Altura, z.Tipo, z.Forma, z.Contorno, Ancho = ancho, Largo = alto, z.Columnas,
                    z.RecubrimientoInferior, z.RecubrimientoLateral, z.RecubrimientoSuperior,
                    EnModelo = Geometria.Girar(z.Contorno, z.Angulo),
                    BarrasTotales = e.Total, BarrasPropias = e.Propias,
                };
            }),
        });

    public static string Resultado(ResultadoZapata r, IReadOnlyList<string> ids, IReadOnlyList<string> avisos, IReadOnlyList<string> errores) =>
        Escribir(new { tipo = "resultado", ids, conjuntos = r.Conjuntos, resumen = r.Resumen, principal = r.Principal, avisos, errores });

    public static string Seleccion(IReadOnlyList<string> ids) => Escribir(new { tipo = "seleccion", ids });

    public static string Aviso(string texto, bool bien = false) => Escribir(new { tipo = "aviso", texto, bien });

    private static string Escribir<T>(T valor) => JsonSerializer.Serialize(valor, Json);

    private sealed class Entrada
    {
        public string? Tipo { get; set; }
        public List<string>? Ids { get; set; }
        public DireccionEntrada? X { get; set; }
        public DireccionEntrada? Y { get; set; }
        public double? Inferior { get; set; }
        public double? Lateral { get; set; }
        public double? Superior { get; set; }
        public bool? IncluirSuperior { get; set; }
        public string? FormaX { get; set; }
        public string? FormaY { get; set; }
        public string? GanchoX { get; set; }
        public string? GanchoY { get; set; }
    }

    private sealed class DireccionEntrada
    {
        public int Numero { get; set; }
        public double Separacion { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter<Terminacion>))]
        public Terminacion Terminacion { get; set; } = Terminacion.Pata90;
    }
}
