namespace CQEING.Despiece.Calculo;

/// <summary>
/// La escala de las vistas del despiece, que se elige en el tablero (#272, decisión 5 del plan
/// docs/planes/2026-11-05-reunion-despiece-287.md): el texto mide siempre lo mismo impreso y lo que cambia es la escala.
/// Es el denominador: 50 es 1:50.
/// </summary>
public static class EscalaVista
{
    /// <summary>Con la que se midieron todas las separaciones del despiece.</summary>
    public const int Base = 15;

    /// <summary>La de la vista larga o el alzado (decisión 27 del plan de #287).</summary>
    public const int PorDefecto = 50;

    /// <summary>La de la sección (decisión 27 del plan de #287).</summary>
    public const int PorDefectoSeccion = 20;

    /// <summary>Lo que acepta Revit para la escala de una vista.</summary>
    public const int Minimo = 1;
    public const int Maximo = 24000;

    /// <summary>Las de la lista del tablero: las de Revit más 1:15, la de antes.</summary>
    public static readonly IReadOnlyList<int> Lista = [1, 2, 5, 10, 15, 20, 25, 50, 75, 100, 200, 500];

    /// <summary>Entera y dentro de los límites; lo que no es un número vale la de por defecto.</summary>
    public static int Limitar(double escala) =>
        double.IsFinite(escala) ? (int)Math.Clamp(Math.Round(escala, MidpointRounding.AwayFromZero), Minimo, Maximo) : PorDefecto;

    /// <summary>
    /// Cuánto crece, en el modelo, lo que se mide en el papel (texto, aire entre textos, huecos entre dibujos): a 1:50 el
    /// mismo texto ocupa 50/15 veces más modelo que a 1:15.
    /// </summary>
    public static double Factor(double escala) => (double)Limitar(escala) / Base;
}
