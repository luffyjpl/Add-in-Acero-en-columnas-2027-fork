namespace CQEING.Despiece.Calculo;

/// <summary>
/// El primer paquete de estribos de una columna, en metros: el estribo principal y las grapas y cajas que van con él, cada
/// una a su altura (en `Col B-3` las de las caras X van debajo del estribo y las de las caras Y encima). La sección de la
/// columna tiene que verlas todas (decisión 44 del plan de #317).
/// </summary>
public static class PrimerPaquete
{
    /// <summary>
    /// <paramref name="piezas"/>: cada estribo, grapa o caja con su huella en planta (dónde está y qué forma tiene; dos
    /// piezas con la misma huella se ven igual en la sección) y su altura. El paquete va desde la más baja hasta la más
    /// alta de las primeras de cada huella, sin pasar de <paramref name="maximo"/> sobre la más baja: lo que solo aparece
    /// más arriba no es del primer paquete.
    /// </summary>
    public static (double Desde, double Hasta) Calcular(IReadOnlyList<(string Huella, double Z)> piezas, double maximo = 0.15)
    {
        double desde = piezas.Min(p => p.Z);
        List<List<double>> porHuella = piezas.GroupBy(p => p.Huella).Select(g => g.Select(p => p.Z).OrderBy(z => z).ToList()).ToList();
        // El paquete siguiente empieza donde una huella se repite por primera vez: así no depende de la medida de la
        // columna ni de la separación de los estribos.
        double siguiente = porHuella.Where(zs => zs.Count > 1).Select(zs => zs[1]).DefaultIfEmpty(double.PositiveInfinity).Min();
        double tope = Math.Min(siguiente, desde + maximo);
        double hasta = porHuella.Select(zs => zs[0]).Where(z => z < tope).DefaultIfEmpty(desde).Max();
        return (desde, hasta);
    }
}
