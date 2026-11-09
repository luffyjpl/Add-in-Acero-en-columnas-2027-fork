namespace CQEING.Despiece.Calculo;

/// <summary>Una forma de estribo de la columna: su clave, su largo y entre qué alturas está, en metros.</summary>
public sealed record FormaEstribo(string Clave, double Largo, double Desde, double Hasta);

/// <summary>
/// Los estribos principales de una columna (#316): Santiago no quiere los textos de los estribos menores (grapas y
/// cajas) en la vista larga. Una forma es menor si en su tramo de altura hay otra más larga.
/// </summary>
public static class EstribosPrincipales
{
    /// <summary>Lo que se pisan dos tramos de altura para contar como el mismo lugar (y no tocarse apenas en el borde).</summary>
    private const double Superpuesto = 0.05;

    /// <summary>Al centímetro: dos largos que difieren menos que esto son el mismo.</summary>
    private const double MismoLargo = 0.01;

    public static IReadOnlyList<string> Filtrar(IReadOnlyList<FormaEstribo> formas) =>
        formas.Where(f => !formas.Any(o => o.Largo > f.Largo + MismoLargo
                && Math.Min(o.Hasta, f.Hasta) - Math.Max(o.Desde, f.Desde) > Superpuesto))
            .Select(f => f.Clave).ToList();
}
