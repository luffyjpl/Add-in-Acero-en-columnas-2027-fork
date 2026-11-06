using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit;

/// <summary>
/// El tamaño de letra y la escala con que se está armando la vista (#261, #272). Se fijan al empezar cada vista previa o
/// creación de vistas (<see cref="Usar"/>); las separaciones que dependen del texto lo leen de aquí. Revit atiende una
/// orden a la vez, así que nunca se mezclan dos tamaños (decisión 5 del plan docs/planes/2026-11-04-ajustes-despiece-266.md).
/// </summary>
internal static class Letra
{
    public static double Mm { get; private set; } = TamanoLetra.PorDefecto;

    /// <summary>
    /// El denominador de la escala de la vista que se está armando: 50 es 1:50. La vista larga (o el alzado) y la sección
    /// tienen cada una la suya (decisión 20 del plan de #287); cada generador elige con <see cref="EnAlzado"/> o
    /// <see cref="EnSeccion"/> antes de armar su vista.
    /// </summary>
    public static int Escala { get; private set; } = EscalaVista.PorDefecto;

    public static int EscalaAlzado { get; private set; } = EscalaVista.PorDefecto;
    public static int EscalaSeccion { get; private set; } = EscalaVista.PorDefectoSeccion;

    public static void Usar(double mm, double escalaAlzado, double escalaSeccion)
    {
        Mm = TamanoLetra.Limitar(mm);
        EscalaAlzado = EscalaVista.Limitar(escalaAlzado);
        EscalaSeccion = EscalaVista.Limitar(escalaSeccion);
        Escala = EscalaAlzado;
    }

    /// <summary>Lo que sigue es la vista larga de la viga o el alzado de la columna.</summary>
    public static void EnAlzado() => Escala = EscalaAlzado;

    /// <summary>Lo que sigue es una sección.</summary>
    public static void EnSeccion() => Escala = EscalaSeccion;

    /// <summary>
    /// Cuánto crecen, en el modelo, las separaciones de texto medidas con 2.5 mm a 1:15: con la letra y con la escala,
    /// porque el texto mide lo mismo en el papel a cualquier escala (decisión 5 del plan de #287).
    /// </summary>
    public static double Factor => TamanoLetra.Factor(Mm) * Papel;

    /// <summary>Cuánto crece lo que se midió en el papel a 1:15 y no depende de la letra: aire y huecos entre dibujos.</summary>
    public static double Papel => EscalaVista.Factor(Escala);

    /// <summary>El tamaño en pies, como lo guarda Revit en <c>TEXT_SIZE</c>.</summary>
    public static double Pies => Mm / 304.8;

    public static string Nombre(string tipo) => TamanoLetra.Nombre(tipo, Mm);
}
