using System.Globalization;

namespace CQEING.Despiece.Calculo;

/// <summary>
/// El tamaño de las letras del despiece, que se elige en el tablero (#261, decisiones 3 a 5 del plan
/// docs/planes/2026-11-04-ajustes-despiece-266.md). En milímetros de papel, como lo mide Revit.
/// </summary>
public static class TamanoLetra
{
    /// <summary>Con el que se midieron todas las separaciones del despiece.</summary>
    public const double Base = 2.5;

    public const double PorDefecto = 3.5;
    public const double Minimo = 2.0;
    public const double Maximo = 5.0;
    public const double Paso = 0.5;

    /// <summary>Al medio milímetro y dentro de los límites; lo que no es un número vale el de por defecto.</summary>
    public static double Limitar(double mm) =>
        double.IsFinite(mm) ? Math.Clamp(Math.Round(mm / Paso, MidpointRounding.AwayFromZero) * Paso, Minimo, Maximo) : PorDefecto;

    /// <summary>Cuánto crecen las separaciones que dependen del texto.</summary>
    public static double Factor(double mm) => Limitar(mm) / Base;

    /// <summary>El nombre del tipo de Revit para ese tamaño: <c>CQ Despiece etiqueta 3.5 mm</c>.</summary>
    public static string Nombre(string tipo, double mm) =>
        $"{tipo} {Limitar(mm).ToString("0.0", CultureInfo.InvariantCulture)} mm";
}
