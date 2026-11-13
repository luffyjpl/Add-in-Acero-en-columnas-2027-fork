namespace CQEING.AreaUtil.Calculo;

/// <summary>
/// El nivel que se propone para el área útil del primer piso (decisión 23 del plan). Antes era el más bajo del modelo,
/// que suele ser la cimentación: las líneas quedaban debajo de la planta del piso 1 y no se veían en ella.
/// </summary>
public static class NivelesLote
{
    /// <summary>
    /// El de la planta activa si hay una; si no, el primero a cota 0 o más arriba (el primer piso); si todos están bajo
    /// cero, el más alto. Null sin niveles.
    /// </summary>
    public static string? PorDefecto(IReadOnlyList<NivelLeido> niveles, string? nivelDeLaPlantaActiva)
    {
        if (nivelDeLaPlantaActiva is not null && niveles.Any(n => n.Id == nivelDeLaPlantaActiva))
            return nivelDeLaPlantaActiva;
        return (niveles.Where(n => n.Cota >= -Poligono.Tolerancia).MinBy(n => n.Cota) ?? niveles.MaxBy(n => n.Cota))?.Id;
    }
}
