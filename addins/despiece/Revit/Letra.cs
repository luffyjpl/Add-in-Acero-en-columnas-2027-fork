using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit;

/// <summary>
/// El tamaño de letra con el que se está armando la vista (#261). Se fija al empezar cada vista previa o creación de
/// vistas (<see cref="Usar"/>); las separaciones que dependen del texto lo leen de aquí. Revit atiende una orden a la vez,
/// así que nunca se mezclan dos tamaños (decisión 5 del plan docs/planes/2026-11-04-ajustes-despiece-266.md).
/// </summary>
internal static class Letra
{
    public static double Mm { get; private set; } = TamanoLetra.PorDefecto;

    public static void Usar(double mm) => Mm = TamanoLetra.Limitar(mm);

    /// <summary>Cuánto crecen las separaciones medidas con 2.5 mm.</summary>
    public static double Factor => TamanoLetra.Factor(Mm);

    /// <summary>El tamaño en pies, como lo guarda Revit en <c>TEXT_SIZE</c>.</summary>
    public static double Pies => Mm / 304.8;

    public static string Nombre(string tipo) => TamanoLetra.Nombre(tipo, Mm);
}
