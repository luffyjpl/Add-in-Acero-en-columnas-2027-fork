namespace CQEING.Despiece.Calculo;

/// <summary>Las etiquetas de estribos de una columna, una por zona, a la izquierda (#227, decisión 14 del plan de columnas).</summary>
public static class EtiquetasEstribos
{
    /// <summary>
    /// La altura de cada etiqueta: la del centro de su zona, salvo que quede a menos de <paramref name="alto"/> de la de
    /// abajo; entonces sube lo justo. Devuelve en el mismo orden en que llegaron.
    /// </summary>
    public static IReadOnlyList<double> SinEncimar(IReadOnlyList<double> centros, double alto)
    {
        var alturas = new double[centros.Count];
        double tope = double.NegativeInfinity;
        foreach (int i in Enumerable.Range(0, centros.Count).OrderBy(i => centros[i]))
        {
            alturas[i] = Math.Max(centros[i], tope + alto);
            tope = alturas[i];
        }
        return alturas;
    }
}
