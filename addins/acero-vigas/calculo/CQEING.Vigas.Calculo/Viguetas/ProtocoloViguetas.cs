using System.Text.Json;
using System.Text.Json.Serialization;
using CQEING.Vigas.Calculo.Porticos;
using CQEING.Vigas.Calculo.Tablero;

namespace CQEING.Vigas.Calculo.Viguetas;

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

public sealed record CerrarViguetas : MensajeViguetas;

public sealed record DesconocidoViguetas : MensajeViguetas;

/// <summary>
/// Lee y escribe los mensajes de «Crear viguetas», con las mismas opciones de JSON que el tablero
/// (<see cref="ProtocoloTablero.Json"/>). Los puntos viajan como [x, y] en mm.
/// </summary>
public static class ProtocoloViguetas
{
    /// <summary>Nunca lanza: un JSON roto da <see cref="DesconocidoViguetas"/>.</summary>
    public static MensajeViguetas Leer(string json)
    {
        Entrada? e;
        try
        {
            e = JsonSerializer.Deserialize<Entrada>(json, ProtocoloTablero.Json);
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
            "cerrar" => new CerrarViguetas(),
            _ => new DesconocidoViguetas(),
        };
    }

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
                    largo = v.Largo, tipo = v.Tipo, borrada = v.Borrada,
                }),
                tipos = p.Tipos,
            },
        });

    public static string Aviso(string texto, bool bien = false) => Escribir(new { tipo = "aviso", texto, bien });

    private static double[] Xy(Punto p) => [Math.Round(p.X, 1), Math.Round(p.Y, 1)];

    private static string Escribir(object o) => JsonSerializer.Serialize(o, ProtocoloTablero.Json);

    private sealed class Entrada
    {
        public string? Tipo { get; set; }
        public string? Id { get; set; }
        public bool? ViguetasComoBorde { get; set; }
        public List<string>? Paneles { get; set; }
        public bool? Continuas { get; set; }
        public bool? Seguir { get; set; }
        public List<string>? QuitarModelo { get; set; }
        public Direccion? Direccion { get; set; }
        public double? EjeAEje { get; set; }
        public double? Ancho { get; set; }
        public List<string>? Borradas { get; set; }
        public string? TipoVigueta { get; set; }
    }
}
