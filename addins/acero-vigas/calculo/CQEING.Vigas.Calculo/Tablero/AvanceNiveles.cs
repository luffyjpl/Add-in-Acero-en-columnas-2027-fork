namespace CQEING.Vigas.Calculo.Tablero;

/// <summary>
/// Punto 14 de Santiago (sheet de observaciones): el selector de nivel de la planta refleja qué pisos ya están
/// construidos. Sale de la marca de terminado de cada pórtico (punto 5), sin marcar el piso a mano.
/// </summary>
public static class AvanceNiveles
{
    /// <summary>La planta con cuántos pórticos tiene cada nivel y cuántos de ellos están en <paramref name="terminados"/>.</summary>
    public static PlantaVista Con(PlantaVista planta, IReadOnlySet<string> terminados) =>
        planta with
        {
            Niveles = planta.Niveles.Select(n =>
            {
                // un pórtico de varias vigas trae un elemento por pieza: se cuenta una vez
                List<string> porticos = n.Elementos.Select(e => e.Portico).Distinct().ToList();
                return n with { Porticos = porticos.Count, Terminados = porticos.Count(terminados.Contains) };
            }).ToList(),
        };
}
