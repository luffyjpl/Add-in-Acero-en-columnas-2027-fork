namespace CQEING.Despiece.Calculo;

/// <summary>
/// Algo que ocupa lugar: de <paramref name="Desde"/> a <paramref name="Hasta"/> a lo largo (de la viga o de la columna) y
/// de <paramref name="Cerca"/> a <paramref name="Lejos"/> hacia afuera, medido desde la línea de su grupo (desde la base
/// si es un obstáculo). Un gancho que vuelve hacia la viga tiene <paramref name="Cerca"/> negativo.
/// </summary>
public sealed record Rectangulo(double Desde, double Hasta, double Cerca, double Lejos);

/// <summary>
/// Lo que se mueve junto: una fila de barras de la viga o una línea de la columna, con sus ganchos, textos y medidas.
/// <paramref name="Minimo"/>: su línea no va más cerca de la base que esto.
/// </summary>
public sealed record GrupoEmpacado(IReadOnlyList<Rectangulo> Partes, double Minimo = double.NegativeInfinity);

/// <summary>
/// Acomoda grupos "lo más cerca posible sin tocar a los otros" (Luciano, 10-11; decisión 4 del plan
/// docs/planes/2026-11-10-despiece-santiago-14-15.md). Cada grupo va, en su turno, al lugar más cercano a la base donde
/// ninguna de sus partes toca a las de los grupos ya puestos ni a los obstáculos. Mira dónde está cada cosa a lo largo:
/// un gancho solo choca con lo que tiene a su misma altura. No usa medidas de ninguna viga ni columna en particular.
/// </summary>
public static class Empacador
{
    private const double Tolerancia = 1e-9;

    /// <summary>
    /// Devuelve, para cada grupo y en el mismo orden, a qué distancia de la base va su línea. <paramref name="enOrden"/>:
    /// cada grupo va más afuera que el anterior (al menos <paramref name="huecoAfuera"/>), como las filas de la viga, que
    /// tienen un orden (piel, bastones, diámetros); sin él, uno chico puede entrar en un hueco que dejaron otros.
    /// </summary>
    public static IReadOnlyList<double> Colocar(IReadOnlyList<GrupoEmpacado> grupos, double base_, double huecoALoLargo, double huecoAfuera,
        bool enOrden = false, IEnumerable<Rectangulo>? obstaculos = null)
    {
        var puestos = new List<Rectangulo>(obstaculos ?? []);
        var lineas = new List<double>();
        double? anterior = null;
        foreach (GrupoEmpacado g in grupos)
        {
            if (g.Partes.Count == 0)
            {
                lineas.Add(Math.Max(base_, g.Minimo));
                continue;
            }
            double minimo = Math.Max(base_ - g.Partes.Min(p => p.Cerca), g.Minimo);
            if (enOrden && anterior is { } a)
                minimo = Math.Max(minimo, a + huecoAfuera);

            // La línea buena es la mínima, o una que deja una parte justo apoyada (con su hueco) sobre algo ya puesto.
            IEnumerable<double> candidatas = new[] { minimo }.Concat(
                from r in g.Partes
                from p in puestos
                where SeCruzanALoLargo(r, p, huecoALoLargo)
                select p.Lejos + huecoAfuera - r.Cerca);
            double linea = candidatas.Where(t => t >= minimo - Tolerancia).OrderBy(t => t)
                .First(t => !g.Partes.Any(r => puestos.Any(p => SeCruzanALoLargo(r, p, huecoALoLargo) && SeCruzanAfuera(r, t, p, huecoAfuera))));
            lineas.Add(linea);
            puestos.AddRange(g.Partes.Select(r => r with { Cerca = r.Cerca + linea, Lejos = r.Lejos + linea }));
            anterior = linea;
        }
        return lineas;
    }

    private static bool SeCruzanALoLargo(Rectangulo r, Rectangulo p, double hueco) =>
        r.Desde < p.Hasta + hueco - Tolerancia && p.Desde < r.Hasta + hueco - Tolerancia;

    private static bool SeCruzanAfuera(Rectangulo r, double linea, Rectangulo p, double hueco) =>
        linea + r.Cerca < p.Lejos + hueco - Tolerancia && p.Cerca < linea + r.Lejos + hueco - Tolerancia;
}
