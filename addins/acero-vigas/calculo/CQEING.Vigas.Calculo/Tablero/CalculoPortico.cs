using CQEING.Vigas.Calculo.Estribos;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Porticos;
using CQEING.Vigas.Calculo.Seccion;
using CQEING.Vigas.Calculo.Vigas;

namespace CQEING.Vigas.Calculo.Tablero;

/// <summary>
/// Todo lo que se calcula de un pórtico con lo que eligió el usuario: barras, bastones, sección y estribos.
/// <paramref name="PorTramo"/> es la configuración de estribos de los tramos marcados (null = la general en todos).
/// <paramref name="SePuedeGenerar"/>: hay solución en las dos capas, la sección no tiene errores y ningún aviso lo impide.
/// <paramref name="Piel"/>: dónde queda la piel en el corte (06-nov), con cuántas caben por lado; null si no se pidió o es vigueta.
/// </summary>
public sealed record PorticoCalculado(
    ResultadoVigas Vigas,
    ResultadoEstribos Estribos,
    EntradaSeccion Seccion,
    IReadOnlyDictionary<string, ConfigEstribos>? PorTramo,
    IReadOnlyList<Aviso> Avisos,
    bool SePuedeGenerar,
    UbicacionPiel? Piel = null);

/// <summary>
/// El cálculo de un pórtico que comparten la vista del tablero (<see cref="ArmadorVista"/>) y la generación en Revit
/// (#85): lo que se genera es exactamente lo que se ve.
/// </summary>
public static class CalculoPortico
{
    /// <summary>Avisos que impiden generar el acero en Revit.</summary>
    private static readonly TipoAviso[] Bloquean = [TipoAviso.SinTraslapoMinimo, TipoAviso.SinSolucion, TipoAviso.DiametroSinReglas];

    /// <summary>Null si algún diámetro elegido no está en las reglas (ver <see cref="DiametrosSinReglas"/>).</summary>
    public static PorticoCalculado? Calcular(PorticoLeido p, OpcionesTablero o, Reglas r)
    {
        if (DiametrosSinReglas(o, r).Count > 0)
            return null;

        ResultadoVigas rv = MotorVigas.Calcular(EntradaDe(p, o, r));
        EntradaSeccion seccion = Seccion(p, o, r);
        IReadOnlyDictionary<string, ConfigEstribos>? porTramo = o.EstribosMarcados is null
            ? null
            : o.TramosMarcados.Distinct().ToDictionary(t => t, _ => o.EstribosMarcados);
        // la vigueta lleva grapa en S en vez del cerrado (decisión 8 del plan de #938)
        TipoEstribo principal = EsVigueta(p, o) ? TipoEstribo.Abierto : TipoEstribo.Cerrado;
        ResultadoEstribos re = MotorEstribos.Calcular(new EntradaEstribos(p.Portico, seccion, o.Estribos, porTramo, principal));
        List<Aviso> avisos = [.. rv.Avisos, .. re.Avisos];
        if (EsVigueta(p, o) && o.Piel is { Cantidad: > 0 })
            avisos.Add(new Aviso(TipoAviso.PielNoPermitida, nameof(Posicion.Piel)));

        bool seccionSinError = re.Seccion.Superior.Error == ErrorCara.Ninguno && re.Seccion.Inferior.Error == ErrorCara.Ninguno;
        bool sePuede = rv.Superior is not null && rv.Inferior is not null && seccionSinError && !avisos.Any(a => Bloquean.Contains(a.Tipo));
        return new PorticoCalculado(rv, re, seccion, porTramo, avisos, sePuede, UbicarPiel(p, o, r));
    }

    /// <summary>
    /// Los diámetros elegidos (barras, bastones y estribos) que no están en las reglas, sin repetir y de menor a mayor.
    /// Con alguno, el reparto de la sección reventaría: no se calcula.
    /// </summary>
    public static IReadOnlyList<int> DiametrosSinReglas(OpcionesTablero o, Reglas r) =>
        o.Superior.Concat(o.Inferior).Select(g => g.Diametro)
            .Concat(new[] { o.Bastones.Superior, o.Bastones.Inferior }.OfType<GrupoBarras>().Select(g => g.Diametro))
            .Append(o.Estribos.Diametro)
            .Concat(o.Piel is { Cantidad: > 0 } ? [o.Piel.Diametro] : [])
            .Concat(o.EstribosMarcados is null ? [] : [o.EstribosMarcados.Diametro])
            .Where(d => !r.PorDiametro.ContainsKey(d)).Distinct().Order().ToList();

    /// <summary>Lo que recibe el motor para el pórtico <paramref name="p"/> con lo que eligió el usuario (también la vista previa, #84).</summary>
    internal static EntradaVigas EntradaDe(PorticoLeido p, OpcionesTablero o, Reglas r) =>
        new(p.Portico, o.Tipo ?? p.Tipo, o.Superior, o.Inferior, r, o.Cara, RecubrimientoPuntas: RecubrimientoPuntas(o),
            Cambios: o.Cambios, Bastones: o.Bastones,
            EstriboEnVoladizo: r.PorDiametro.TryGetValue(o.Estribos.Diametro, out ReglaDiametro? estribo) ? (int)Math.Ceiling(estribo.Diametro) : 0,
            Piel: o.Piel is { Cantidad: > 0 } && UbicarPiel(p, o, r) is { CabenPorLado: > 0 } u ? new GrupoBarras(Math.Min(o.Piel!.Cantidad, u.CabenPorLado), o.Piel.Diametro) : null);

    /// <summary>Lo que traen las puntas si el tablero pide menos: los 5 cm de la maqueta.</summary>
    private const int RecubrimientoPuntasMinimo = 50;

    /// <summary>
    /// De la punta de las barras (la cara de afuera de su gancho) a la cara del pórtico, en mm: el recubrimiento de los
    /// lados del paso 1, sin bajar de <see cref="RecubrimientoPuntasMinimo"/>. Con los 5 cm fijos, la viga de cimentación
    /// de 8 cm quedaba con la pata de arriba a 45 mm de la punta (07-nov, medido en Revit en la 925256).
    /// </summary>
    private static int RecubrimientoPuntas(OpcionesTablero o) =>
        Math.Max(RecubrimientoPuntasMinimo, (int)Math.Round((o.RecLadosCm ?? o.RecubrimientoCm) * 10));

    /// <summary>
    /// Dónde cae la piel en el corte con el tramo más bajo del pórtico (06-nov); null si es vigueta o su diámetro no está en
    /// las reglas. Con cantidad 0, o sin piel en las opciones, se calcula igual para decir cuántas caben; con 0 caben, la
    /// ubicación se devuelve igual para que la vista diga "caben 0".
    /// </summary>
    internal static UbicacionPiel? UbicarPiel(PorticoLeido p, OpcionesTablero o, Reglas r)
    {
        // sin piel en las opciones (un pórtico recién abierto o un estado guardado de antes), con el #4 que la página trae
        // por defecto (Juan, 06-nov: al abrir, el + no dejaba subir hasta mover otra cosa)
        GrupoBarras piel = o.Piel ?? new GrupoBarras(0, 4);
        if (EsVigueta(p, o) || !r.PorDiametro.ContainsKey(piel.Diametro))
            return null;
        EntradaSeccion seccion = Seccion(p, o, r);
        return RepartidorPiel.Ubicar(seccion, RepartidorFilas.Repartir(seccion), piel, p.Tramos.Min(t => t.Alto));
    }

    /// <summary>La sección del pórtico con los recubrimientos (cm en la página, mm en el motor) y las filas del paso 1.</summary>
    internal static EntradaSeccion Seccion(PorticoLeido p, OpcionesTablero o, Reglas r)
    {
        var rec = new Recubrimientos((o.RecArribaCm ?? o.RecubrimientoCm) * 10, (o.RecAbajoCm ?? o.RecubrimientoCm) * 10,
            (o.RecLadosCm ?? o.RecubrimientoCm) * 10);
        return new EntradaSeccion(p.Tramos[0].Ancho, p.Tramos[0].Alto, rec, o.Estribos.Diametro, o.Superior, o.Inferior, r,
            (o.SeparacionFilasCm ?? 0) * 10, o.MaximoFilas, PermiteUnaBarra: EsVigueta(p, o));
    }

    /// <summary>Vigueta, con lo que haya corregido el usuario con el chip (#940) por encima de lo detectado (#939).</summary>
    private static bool EsVigueta(PorticoLeido p, OpcionesTablero o) => (o.Elemento ?? p.Elemento) == Elemento.Vigueta;
}
