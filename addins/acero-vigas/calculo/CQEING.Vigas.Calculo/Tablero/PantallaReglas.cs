using System.Globalization;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Vigas.Calculo.Tablero;

// La pantalla Reglas, aparte del resto del tablero para que zapatas la enlace (decisión 39 del plan
// docs/planes/2026-11-05-correcciones-zapatas-reunion.md). Movido sin cambios de VistaTablero.cs y ArmadorVista.cs.

/// <summary>
/// La pantalla Reglas, con los textos de sus campos (m y kg/m, como <c>RULES0</c> de la maqueta).
/// <paramref name="Problema"/> dice por qué se usan las de base, si pasó. <paramref name="RedondeoGanchoCm"/>: "0", "5" o
/// "10"; null (una página que no lo manda) deja el de las reglas guardadas.
/// </summary>
public sealed record VistaReglas(IReadOnlyList<ReglaVista> PorDiametro, string Multiplo, string DistanciaLibreCm, bool Varilla12, string? Problema,
    string? RedondeoGanchoCm = null);

/// <summary>
/// Una fila de la tabla de reglas: traslapo, gancho de 90°, gancho de 180° (<paramref name="G180"/>, el de los estribos
/// de 180°: tarea 6 del plan de 22-oct), estribo de 135° y peso por metro.
/// </summary>
public sealed record ReglaVista(string D, string T, string G, string G180, string E, string K);

/// <summary>De las reglas a los textos de la pantalla Reglas y de vuelta.</summary>
public static class PantallaReglas
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>La pantalla Reglas: los valores en m y kg/m con los decimales de <c>RULES0</c> de la maqueta.</summary>
    public static VistaReglas De(ReglasCargadas c)
    {
        Reglas r = c.Reglas;
        var filas = Modelo.Reglas.DiametrosEnPantalla.Where(r.PorDiametro.ContainsKey).Select(d =>
        {
            ReglaDiametro x = r.PorDiametro[d];
            return new ReglaVista($"#{d}", x.Traslapo is int t ? F(t / 1000.0, "0.00") : "", F(x.Gancho90.B / 1000.0, "0.000"),
                F(x.Gancho180.B / 1000.0, "0.000"), F(x.Estribo135.B / 1000.0, "0.000"), F(x.KgPorMetro, "0.000"));
        }).ToList();
        return new VistaReglas(filas, F(r.Modulo / 1000.0, "0.00"), F(r.DistanciaLibre / 10, "0.##"), r.Varilla == 12000, c.Problema,
            F(r.RedondeoGancho / 10.0, "0"));
    }

    /// <summary>
    /// Pasa los textos de la pantalla Reglas a <see cref="Modelo.Reglas"/> en mm, encima de <paramref name="base"/> (lo
    /// que la pantalla no muestra, como el #2, queda igual). Un texto que no es número pasa a 0 para que
    /// <see cref="Modelo.Reglas.Validar"/> lo rechace con su mensaje; un traslapo vacío queda sin traslapo.
    /// </summary>
    public static Reglas ReglasDe(VistaReglas v, Reglas @base)
    {
        var porDiametro = new Dictionary<int, ReglaDiametro>(@base.PorDiametro);
        foreach (ReglaVista fila in v.PorDiametro)
        {
            if (!int.TryParse(fila.D.TrimStart('#'), out int d) || !porDiametro.TryGetValue(d, out ReglaDiametro? x))
                continue;
            porDiametro[d] = x with
            {
                Traslapo = string.IsNullOrWhiteSpace(fila.T) ? null : Mm(fila.T),
                Gancho90 = x.Gancho90 with { B = Mm(fila.G) },
                Gancho180 = x.Gancho180 with { B = Mm(fila.G180) },
                Estribo135 = x.Estribo135 with { B = Mm(fila.E) },
                KgPorMetro = Numero(fila.K) ?? 0,
            };
        }
        return @base with
        {
            PorDiametro = porDiametro,
            Modulo = Mm(v.Multiplo),
            DistanciaLibre = (Numero(v.DistanciaLibreCm) ?? 0) * 10,
            Varilla = v.Varilla12 ? 12000 : 6000,
            RedondeoGancho = v.RedondeoGanchoCm is null ? @base.RedondeoGancho : (int)Math.Round((Numero(v.RedondeoGanchoCm) ?? -1) * 10),
        };
    }

    private static string F(double v, string formato) => v.ToString(formato, Inv);

    private static double? Numero(string? texto) =>
        double.TryParse(texto?.Trim().Replace(',', '.'), NumberStyles.Float, Inv, out double v) ? v : null;

    private static int Mm(string texto) => (int)Math.Round((Numero(texto) ?? 0) * 1000);
}
