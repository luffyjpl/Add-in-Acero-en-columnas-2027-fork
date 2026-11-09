namespace practicacq.Modelo;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lo que L2 arma para pedirle a J3 que genere el armado real en Revit: el
/// PlanArmado tal como quedó configurado en la ventana (mismo objeto que
/// arma <c>BotonExportar_Click</c>, ver <c>ConstruirPlanArmado</c>) más si
/// hay que borrar también el acero que no generó este add-in en esas
/// columnas.
///
/// Vive en Modelo/ y no tiene una sola línea de Autodesk.Revit: la ventana
/// lo arma sin abrir Revit, y quién lo ejecuta (<c>ComandoEstribos</c>) es
/// quien sabe traducirlo a la API real (ver <c>Logica/GeneradorArmado</c>).
/// </summary>
public sealed class OpcionesArmado
{
    public PlanArmado Plan { get; set; } = new();

    /// <summary>
    /// El acero que generó este add-in en una corrida anterior se reemplaza
    /// siempre (#145; hasta el 22-oct, por el #2, se apilaba). Esto agrega
    /// borrar también el que NO generó el add-in (el modelado a mano, o el de
    /// otro add-in) en esas columnas: la casilla "Borrar TODO el armado
    /// existente…", destildada por defecto.
    /// </summary>
    public bool BorrarArmadoAjeno { get; set; }
}

public sealed class ResultadoArmado
{
    /// <summary>Elementos de acero borrados antes de generar: los de una corrida anterior del add-in y, con <see cref="OpcionesArmado.BorrarArmadoAjeno"/>, también los ajenos.</summary>
    public int Eliminados { get; set; }

    /// <summary>Rebar creados -- cada grupo (el uniforme, o cada una de las tres zonas de un confinado) cuenta aparte.</summary>
    public int GruposCreados { get; set; }

    /// <summary>Tramos que no generaron nada por un problema puntual (sin recubrimiento, sin host, geometría que no encaja...).</summary>
    public int TramosOmitidos { get; set; }

    public List<string> Avisos { get; } = new();

    /// <summary>
    /// Los tramos (id de la columna del plan, índice del tramo) en los que se
    /// creó al menos un conjunto de estribos. Decisión 7 de
    /// docs/planes/2026-11-07-estribos-centrados-y-fallas-santiago.md: lo que
    /// se marca y se guarda como generado sale de acá, no del plan pedido.
    /// </summary>
    public HashSet<(string Columna, int Tramo)> TramosGenerados { get; } = new();

    /// <summary>Las columnas (id del plan) en las que se creó al menos una longitudinal.</summary>
    public HashSet<string> ColumnasConLongitudinal { get; } = new();

    /// <summary>Si en la columna se creó algo: estribos en algún tramo o alguna longitudinal.</summary>
    public bool ColumnaArmada(string idColumna) =>
        ColumnasConLongitudinal.Contains(idColumna) || TramosGenerados.Any(t => t.Columna == idColumna);

    /// <summary>
    /// El plan con solo lo que se creó: sin las columnas que no se armaron, y
    /// en las armadas sin los tramos omitidos. El tramo contra zapata nunca
    /// lleva estribos, así que se conserva si la columna se armó.
    /// </summary>
    public PlanArmado SoloLoGenerado(PlanArmado plan) => new()
    {
        GeneradoEl = plan.GeneradoEl,
        Columnas = plan.Columnas.Where(c => ColumnaArmada(c.Id)).Select(c => new ColumnaPlan
        {
            Id = c.Id,
            Contexto = c.Contexto,
            Recubrimiento = c.Recubrimiento,
            Longitudinal = c.Longitudinal,
            Tramos = c.Tramos.Select((t, i) =>
                t is not null && (TramosGenerados.Contains((c.Id, i)) ||
                                  (c.Contexto is not null && i < c.Contexto.Tramos.Count && c.Contexto.Tramos[i].Tipo == TipoTramo.ContraZapata))
                    ? t
                    : null).ToList(),
        }).ToList(),
    };

    /// <summary>
    /// <see cref="SoloLoGenerado"/> del plan pedido, ya calculado por quien
    /// generó: es lo que la ventana marca como armado. Null si no se llegó a
    /// calcular (la ventana usa entonces el plan pedido).
    /// </summary>
    public PlanArmado? PlanGenerado { get; set; }

    /// <summary>Lo que quedó creado, para no tener que ir a buscarlo a Propiedades.</summary>
    public List<string> Detalles { get; } = new();

    /// <summary>
    /// Posiciones Z (pies, coordenadas internas de Revit) de cada estribo
    /// principal realmente creado, agrupadas por (IdElementoHost, Indice del
    /// tramo). <see cref="practicacq.Logica.GeneradorLongitudinal"/> (J4) genera los
    /// estribos menores en estas mismas posiciones en vez de calcular las
    /// suyas propias -- issue #32: William confirmó que van en cada estribo
    /// principal, no solo en los extremos del tramo.
    /// </summary>
    public Dictionary<(string IdElementoHost, int IndiceTramo), List<double>> PosicionesEstribosPorTramo { get; } = new();

    /// <summary>
    /// El paquete de estribos (principal + menores apilados) con el que
    /// <see cref="practicacq.Logica.GeneradorArmado"/> repartió cada tramo:
    /// <see cref="practicacq.Logica.GeneradorLongitudinal"/> pone cada estribo
    /// menor a la altura de su nivel -- docs/planes/2026-10-20-diseno-estribos-menores-visual-y-64.md,
    /// sección 2. Solo están los tramos libres de columnas con estribos menores.
    /// </summary>
    public Dictionary<(string IdElementoHost, int IndiceTramo), PaqueteEstribos.Paquete> PaquetePorTramo { get; } = new();
}
