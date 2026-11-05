namespace CQEING.Despiece.Calculo;

/// <summary>A traslapa en unos tramos y B en los otros; en la sección van intercaladas (#228).</summary>
public enum GrupoTraslapo { A, B }

/// <summary>De dónde salió el grupo: del registro del add-in de columnas o de la altura de los traslapos.</summary>
public enum FuenteGrupos { Registro, Altura }

/// <summary>Una varilla de la columna: su eje en planta y la altura del centro de cada traslapo, en metros.</summary>
public record VarillaLeida(double X, double Y, IReadOnlyList<double> Traslapos);

/// <summary>El grupo de cada varilla, en el mismo orden en que llegaron. <paramref name="Coinciden"/>: las dos fuentes dan lo mismo.</summary>
public record ResultadoGrupos(IReadOnlyList<GrupoTraslapo> Grupos, FuenteGrupos Fuente, bool Coinciden);

/// <summary>
/// El grupo A/B de cada varilla (decisión 4 del plan docs/planes/2026-10-31-despiece-columnas-226-228.md). El add-in de
/// columnas no lo guarda por varilla, pero sigue una regla fija (<c>ConfigLongitudinal.Anillo</c>): el perímetro se recorre
/// desde la esquina inferior izquierda, contra reloj visto desde arriba, y las posiciones pares son A. Medido en Revit, las
/// del mismo grupo traslapan a la misma altura (hallazgo 15: 8 de 8).
/// </summary>
public static class GruposTraslapo
{
    /// <summary>Dos traslapos a menos de esto, en metros, están a la misma altura.</summary>
    private const double MismaAltura = 0.05;

    /// <summary>Los índices de las varillas en el orden del anillo.</summary>
    public static IReadOnlyList<int> Anillo(IReadOnlyList<VarillaLeida> varillas)
    {
        double cx = varillas.Average(v => v.X), cy = varillas.Average(v => v.Y);
        int inicio = Enumerable.Range(0, varillas.Count).MinBy(i => varillas[i].X + varillas[i].Y);
        double a0 = Math.Atan2(varillas[inicio].Y - cy, varillas[inicio].X - cx);
        return Enumerable.Range(0, varillas.Count).OrderBy(i =>
        {
            double a = Math.Atan2(varillas[i].Y - cy, varillas[i].X - cx) - a0;
            return a < -1e-9 ? a + 2 * Math.PI : a;
        }).ToList();
    }

    /// <summary>
    /// Con el registro, si dice tantas varillas como las que hay, manda el anillo; si no, la altura de los traslapos. Null
    /// si no hay cómo saberlo: sin registro y con todas traslapando a la misma altura (o sin traslapos).
    /// </summary>
    public static ResultadoGrupos? Asignar(IReadOnlyList<VarillaLeida> varillas, int? varillasDelRegistro)
    {
        if (varillas.Count == 0)
            return null;
        IReadOnlyList<int> anillo = Anillo(varillas);
        var porAnillo = new GrupoTraslapo[varillas.Count];
        for (int i = 0; i < anillo.Count; i++)
            porAnillo[anillo[i]] = i % 2 == 0 ? GrupoTraslapo.A : GrupoTraslapo.B;
        GrupoTraslapo[]? porAltura = PorAltura(varillas, anillo[0]);

        if (varillasDelRegistro == varillas.Count)
            return new ResultadoGrupos(porAnillo, FuenteGrupos.Registro, porAltura is null || porAltura.SequenceEqual(porAnillo));
        return porAltura is null ? null : new ResultadoGrupos(porAltura, FuenteGrupos.Altura, porAltura.SequenceEqual(porAnillo));
    }

    /// <summary>A, las que traslapan donde la primera del anillo; B, las demás. Null si no hay exactamente dos alturas.</summary>
    private static GrupoTraslapo[]? PorAltura(IReadOnlyList<VarillaLeida> varillas, int primera)
    {
        bool Iguales(IReadOnlyList<double> a, IReadOnlyList<double> b) =>
            a.Count == b.Count && a.Order().Zip(b.Order()).All(p => Math.Abs(p.First - p.Second) < MismaAltura);

        IReadOnlyList<double> deA = varillas[primera].Traslapos;
        if (deA.Count == 0)
            return null;
        GrupoTraslapo[] grupos = varillas.Select(v => Iguales(v.Traslapos, deA) ? GrupoTraslapo.A : GrupoTraslapo.B).ToArray();
        List<VarillaLeida> deB = varillas.Where((_, i) => grupos[i] == GrupoTraslapo.B).ToList();
        bool dosAlturas = deB.Count > 0 && deB.All(v => v.Traslapos.Count > 0 && Iguales(v.Traslapos, deB[0].Traslapos));
        return dosAlturas ? grupos : null;
    }
}
