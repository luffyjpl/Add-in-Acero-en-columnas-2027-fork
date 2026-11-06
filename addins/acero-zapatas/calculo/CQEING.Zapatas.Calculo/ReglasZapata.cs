using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo;

/// <summary>Las reglas propias de zapatas sobre la tabla de vigas (#249, decisiones 12, 13 y 22 de los planes).</summary>
public static class ReglasZapata
{
    /// <summary>Por debajo de 12 diámetros la barra no desarrolla bien y lo revisa el diseñador (Juan, 04-nov).</summary>
    public const double DiametrosMinimosPata = 12;

    /// <summary>La longitud de corte sube a múltiplos de 5 cm, y el peso sale de ahí (decisión 13).</summary>
    public const int RedondeoCorte = 50;

    /// <summary>El largo que suma un gancho del cuadro, en mm, con la misma clase que vigas y sin redondeo propio.</summary>
    public static int LargoGancho(Reglas reglas, int numero, Terminacion t) => t switch
    {
        Terminacion.Gancho135 or Terminacion.Cerrada => Reglas.LargoGancho(reglas.PorDiametro[numero], TipoGancho.De135),
        Terminacion.Gancho180 => Reglas.LargoGancho(reglas.PorDiametro[numero], TipoGancho.De180),
        _ => 0,
    };

    public static double LongitudCorte(double largoNeto) => Math.Ceiling(Math.Round(largoNeto, 3) / RedondeoCorte) * RedondeoCorte;

    /// <summary>
    /// Si el gancho elegido no corresponde a la tabla, el texto del aviso; si corresponde, null (decisión 22). Compara el
    /// ángulo y el diámetro de doblado del cuadro para ese número de barra, no el largo de la pata.
    /// </summary>
    /// <param name="grados">Ángulo del gancho elegido en Revit.</param>
    /// <param name="doblado">Diámetro de doblado que usará Revit, en mm; null si no se conoce.</param>
    public static string? AvisoTabla(Reglas reglas, int numero, int grados, double? doblado)
    {
        if (!reglas.PorDiametro.TryGetValue(numero, out ReglaDiametro? r))
            return $"El cuadro de ganchos no trae la barra #{numero}.";
        if (grados is not (90 or 135 or 180))
            return $"El gancho de {grados}° no está en el cuadro de ganchos (90°, 135° y 180°).";
        TipoGancho tipo = grados switch { 90 => TipoGancho.De90, 135 => TipoGancho.De135, _ => TipoGancho.De180 };
        double delCuadro = Reglas.DobladoGancho(r, tipo);
        // Se avisa si dobla más cerrado que el cuadro; un doblado mayor no incumple el mínimo.
        return doblado is double d && d < delCuadro - 1
            ? $"El gancho de {grados}° de la barra #{numero} dobla a {d:0.#} mm y el cuadro pide al menos {delCuadro:0.#} mm."
            : null;
    }
}
