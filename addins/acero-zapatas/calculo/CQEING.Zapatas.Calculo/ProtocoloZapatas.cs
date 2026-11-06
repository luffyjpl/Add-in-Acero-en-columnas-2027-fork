using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CQEING.Vigas.Calculo.Generacion;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Tablero;

namespace CQEING.Zapatas.Calculo;

/// <summary>Un mensaje de la página del tablero de zapatas (#252).</summary>
public abstract record MensajeZapatas;

public sealed record ListoZapatas : MensajeZapatas;

/// <summary>Calcula la configuración. <paramref name="Tipos"/> sirve para la cola de los ganchos que dibuja la vista previa.</summary>
public sealed record CalcularZapatas(IReadOnlyList<string> Ids, ConfigParrillas Config, TiposElegidos Tipos) : MensajeZapatas;

/// <summary>Toma las zapatas seleccionadas en Revit o pide elegirlas.</summary>
public sealed record SeleccionarEnRevit : MensajeZapatas;

public sealed record MostrarEnRevit(IReadOnlyList<string> Ids) : MensajeZapatas;

/// <summary>
/// Lo que eligió el modelador en el catálogo, por parrilla y dirección (decisión 30 del plan de 2026-11-05): el id de la forma,
/// o null para la automática, y el del tipo de gancho para 135° y 180°. Sin los de la superior, se usan los de la inferior.
/// </summary>
public sealed record TiposElegidos(string? FormaX, string? FormaY, string? GanchoX, string? GanchoY,
    string? FormaSuperiorX = null, string? FormaSuperiorY = null, string? GanchoSuperiorX = null, string? GanchoSuperiorY = null)
{
    public static TiposElegidos Automaticos { get; } = new(null, null, null, null);

    public string? Forma(Parrilla p, Direccion d) => p == Parrilla.Superior
        ? (d == Direccion.X ? FormaSuperiorX ?? FormaX : FormaSuperiorY ?? FormaY)
        : (d == Direccion.X ? FormaX : FormaY);

    public string? Gancho(Parrilla p, Direccion d) => p == Parrilla.Superior
        ? (d == Direccion.X ? GanchoSuperiorX ?? GanchoX : GanchoSuperiorY ?? GanchoY)
        : (d == Direccion.X ? GanchoX : GanchoY);
}

/// <summary>
/// Genera el acero con la configuración y los tipos elegidos. Sin <paramref name="Forzar"/>, si hay choques no se genera y
/// se contesta <see cref="ProtocoloZapatas.Choques"/>; con él («Aplicar de todos modos»), se genera igual. Como vigas
/// (decisión 35 del plan de 2026-11-05).
/// </summary>
public sealed record AplicarZapatas(IReadOnlyList<string> Ids, ConfigParrillas Config, TiposElegidos Tipos, bool Forzar = false) : MensajeZapatas;

public sealed record CerrarZapatas : MensajeZapatas;

/// <summary>«Guardar y bloquear» en la pantalla Reglas: las reglas generales, compartidas con vigas (decisión 38).</summary>
public sealed record GuardarReglasZapatas(VistaReglas Reglas) : MensajeZapatas;

public sealed record DesconocidoZapatas : MensajeZapatas;

/// <summary>
/// La forma de una barra de parrilla, por su geometría (H16 del plan de 2026-11-05). <see cref="Cerrada"/>: rectángulo
/// cerrado de estribo con 135° en las dos puntas (decisión 43, H21).
/// </summary>
public enum ClaseForma { Recta, L, U, Cerrada }

/// <summary>
/// Una forma del proyecto para el catálogo, con sus curvas como polilíneas [x, y] en mm. Solo van las que tienen
/// <paramref name="Clase"/>: las que Revit acepta para una parrilla (decisión 37).
/// </summary>
public sealed record FormaCatalogo(string Id, string Nombre, IReadOnlyList<IReadOnlyList<double[]>> Curvas, ClaseForma Clase);

/// <summary>Un tipo de gancho del proyecto para el catálogo; la extensión va en diámetros.</summary>
public sealed record GanchoCatalogo(string Id, string Nombre, int Grados, double Extension);

/// <summary>
/// Un número de barra disponible en el proyecto, con los largos B del cuadro de ganchos en mm. <paramref name="Colas"/>:
/// por id de tipo de gancho, el largo de la cola recta que deja Revit en esta barra (H13), para la vista previa.
/// <paramref name="DobladoEstribo"/> y <paramref name="ColaEstribo"/>: el doblez de estribo del tipo de barra y la cola del
/// gancho de 135° de estribo que usa la barra cerrada (decisión 43, H21), en mm.
/// </summary>
public sealed record BarraCatalogo(int Numero, string Nombre, double Diametro, double Doblado, double Gancho90, double Gancho135, double Gancho180,
    IReadOnlyDictionary<string, double>? Colas = null, double? DobladoEstribo = null, double? ColaEstribo = null);

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
            "calcular" when ids.Count > 0 && Config(e) is { } c => new CalcularZapatas(ids, c, Tipos(e)),
            "seleccionar" => new SeleccionarEnRevit(),
            "mostrar" when ids.Count > 0 => new MostrarEnRevit(ids),
            "aplicar" when ids.Count > 0 && Config(e) is { } c => new AplicarZapatas(ids, c, Tipos(e), e.Forzar ?? false),
            "cerrar" => new CerrarZapatas(),
            "guardarReglas" when e.Reglas is { PorDiametro: not null } r => new GuardarReglasZapatas(r),
            _ => new DesconocidoZapatas(),
        };
    }

    private static ConfigParrillas? Config(Entrada e) => e.X is null || e.Y is null ? null : new ConfigParrillas(
        LeerDireccion(e.X), LeerDireccion(e.Y),
        new Recubrimientos(e.Inferior ?? ValoresPorDefecto.RecubrimientoMm, e.Lateral ?? ValoresPorDefecto.RecubrimientoMm, e.Superior ?? ValoresPorDefecto.RecubrimientoMm),
        e.IncluirSuperior ?? true, LargoPata: e.LargoPata ?? LargoPata.Cuadro);

    private static ConfigDireccion LeerDireccion(DireccionEntrada d) => new(d.Numero, d.Separacion, d.Terminacion, d.Patas,
        Superior: d.Superior is { } s ? new Extremos(s.Terminacion, s.Patas) : null);

    private static TiposElegidos Tipos(Entrada e) =>
        new(e.FormaX, e.FormaY, e.GanchoX, e.GanchoY, e.FormaSuperiorX, e.FormaSuperiorY, e.GanchoSuperiorX, e.GanchoSuperiorY);

    /// <summary>
    /// La configuración con lo elegido en el catálogo, para calcular y para aplicar lo mismo (paso 6 del plan de
    /// 2026-11-05): el doblado de cada barra del proyecto; con la pata de 90° y una forma elegida, las patas de esa forma
    /// (decisión 37: recta ninguna, L al inicio, U las dos); con 135° o 180°, la cola que deja Revit con ese gancho (H13).
    /// </summary>
    public static ConfigParrillas ConTipos(ConfigParrillas c, TiposElegidos tipos, CatalogoZapatas catalogo)
    {
        return c with
        {
            X = Elegir(c.X, Direccion.X),
            Y = Elegir(c.Y, Direccion.Y),
            Doblados = catalogo.Barras.ToDictionary(b => b.Numero, b => b.Doblado),
            DobladosEstribo = catalogo.Barras.Where(b => b.DobladoEstribo is > 0).ToDictionary(b => b.Numero, b => b.DobladoEstribo!.Value),
        };

        ConfigDireccion Elegir(ConfigDireccion d, Direccion eje)
        {
            Extremos inferior = ConElegido(d.En(Parrilla.Inferior), d.Numero, Parrilla.Inferior, eje);
            Extremos superior = ConElegido(d.En(Parrilla.Superior), d.Numero, Parrilla.Superior, eje);
            return d with { Terminacion = inferior.Terminacion, Patas = inferior.Patas, Cola = inferior.Cola, Superior = superior };
        }

        Extremos ConElegido(Extremos e, int numero, Parrilla p, Direccion eje)
        {
            string? forma = tipos.Forma(p, eje), gancho = tipos.Gancho(p, eje);
            return e.Terminacion switch
            {
                Terminacion.Pata90 when catalogo.Formas.FirstOrDefault(f => f.Id == forma) is { } f => e with
                {
                    Patas = f.Clase switch { ClaseForma.Recta => Patas.Ninguna, ClaseForma.L => Patas.Inicio, _ => Patas.Ambas },
                },
                // La cerrada usa siempre el gancho de 135° de estribo (decisión 43).
                Terminacion.Cerrada when catalogo.Barras.FirstOrDefault(b => b.Numero == numero)?.ColaEstribo is double cola and > 0 => e with { Cola = cola },
                Terminacion.Gancho135 or Terminacion.Gancho180 when gancho is not null
                    && catalogo.Barras.FirstOrDefault(b => b.Numero == numero)?.Colas?.GetValueOrDefault(gancho) is double cola and > 0 => e with { Cola = cola },
                _ => e,
            };
        }
    }

    /// <param name="existentes">Por zapata, cuántas barras tiene y cuántas son del add-in.</param>
    /// <param name="reglas">La pantalla Reglas con las reglas cargadas (decisión 38); sin ellas, las de base.</param>
    public static string Datos(string modelo, IReadOnlyList<ZapataLeida> zapatas, IReadOnlyList<IReadOnlyList<string>> grupos, CatalogoZapatas catalogo,
        IReadOnlyDictionary<string, (int Total, int Propias)> existentes, ReglasCargadas? reglas = null) =>
        Escribir(new
        {
            tipo = "datos",
            modelo,
            grupos,
            catalogo,
            reglas = PantallaReglas.De(reglas ?? new ReglasCargadas(Reglas.Generales, null)),
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

    /// <summary>
    /// El cálculo para la página, con la geometría ya hecha como en vigas (V1 a V4 del plan de 2026-11-05): cada barra
    /// como trazo [x, y, z] en mm con el color de su diámetro, y los choques como puntos con su texto.
    /// </summary>
    public static string Resultado(ResultadoZapata r, IReadOnlyList<string> ids, IReadOnlyList<string> avisos, IReadOnlyList<string> errores,
        IReadOnlyList<Trazo> trazos, IReadOnlyList<ChoqueZapata> choques) =>
        Escribir(new
        {
            tipo = "resultado", ids, conjuntos = r.Conjuntos, resumen = r.Resumen, principal = r.Principal, avisos, errores,
            trazos = trazos.Select(t => new
            {
                t.Pieza, t.Numero, t.Diametro, Color = ColoresDiametro.De(t.Numero),
                Puntos = t.Puntos.Select(Coordenadas),
            }),
            choques = choques.Select(c => new { Punto = Coordenadas(c.Punto), c.A, c.B, c.Texto, c.SeMeteMm }),
        });

    /// <summary>
    /// <c>{"tipo":"choques","cantidad":…}</c>: «Aplicar» sin forzar no generó porque hay choques; la página pregunta
    /// «Revisar» o «Aplicar de todos modos» (decisión 35, como vigas).
    /// </summary>
    public static string Choques(int cantidad) => Escribir(new { tipo = "choques", cantidad });

    private static double[] Coordenadas(Punto3 p) => [Math.Round(p.X, 1), Math.Round(p.Y, 1), Math.Round(p.Z, 1)];

    /// <summary>
    /// Las reglas que dejaría «Guardar y bloquear», encima de <paramref name="actuales"/> (lo que la pantalla no muestra
    /// queda igual), o el primer error de validación, como en vigas.
    /// </summary>
    public static (Reglas? Nuevas, string? Error) ReglasAGuardar(VistaReglas pantalla, Reglas actuales)
    {
        Reglas nuevas = PantallaReglas.ReglasDe(pantalla, actuales);
        IReadOnlyList<string> errores = nuevas.Validar();
        return errores.Count > 0 ? (null, errores[0]) : (nuevas, null);
    }

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
        public LargoPata? LargoPata { get; set; }
        public string? FormaX { get; set; }
        public string? FormaY { get; set; }
        public string? GanchoX { get; set; }
        public string? GanchoY { get; set; }
        public string? FormaSuperiorX { get; set; }
        public string? FormaSuperiorY { get; set; }
        public string? GanchoSuperiorX { get; set; }
        public string? GanchoSuperiorY { get; set; }
        public bool? Forzar { get; set; }
        public VistaReglas? Reglas { get; set; }
    }

    private class ExtremosEntrada
    {
        [JsonConverter(typeof(JsonStringEnumConverter<Terminacion>))]
        public Terminacion Terminacion { get; set; } = Terminacion.Pata90;
        public Patas Patas { get; set; } = Patas.Ambas;
    }

    private sealed class DireccionEntrada : ExtremosEntrada
    {
        public int Numero { get; set; }
        public double Separacion { get; set; }
        public ExtremosEntrada? Superior { get; set; }
    }
}
