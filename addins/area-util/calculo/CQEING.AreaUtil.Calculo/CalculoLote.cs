using System.Globalization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Los datos de la ficha reglamentaria de la curaduría (min 47–49). Lo que no se llena no genera avisos.</summary>
public sealed record Ficha(double? Imo = null, double? Imc = null, int? Pisos = null, double? LoteMinimo = null, double? FrenteMinimo = null);

/// <summary>Un aviso de la columna derecha. Si <paramref name="Bloquea"/>, no se puede generar en Revit.</summary>
public sealed record Aviso(string Texto, bool Bloquea);

/// <summary>Las áreas en m², el largo del frente en m y lo que permite la ficha (null si falta el dato).</summary>
public sealed record Resumen(double AreaLote, double AreaUtil, double AreaRetiros, double Frente,
    double? OcupacionMaxima, double? ConstruccionMaxima, double? ConstruccionPosible);

/// <summary>Lo que llena el usuario. <paramref name="Tipos"/>: los tipos cambiados a mano, o null para deducirlos del frente.</summary>
/// <param name="RetirosVacios">Los retiros que quedaron vacíos en la página: se toman como 0 y se avisa (decisión 24).</param>
public sealed record EntradaLote(IReadOnlyList<Punto> Puntos, int Frente, IReadOnlyList<TipoLado>? Tipos, Retiros Retiros, Ficha Ficha,
    IReadOnlyList<TipoLado>? RetirosVacios = null);

public sealed record ResultadoLote(Poligono? Lote, IReadOnlyList<TipoLado> Tipos, Poligono? Util, IReadOnlyList<Trazo> Trazos,
    Resumen? Resumen, IReadOnlyList<Aviso> Avisos)
{
    public bool SePuedeGenerar => Lote is not null && Util is not null && !Avisos.Any(a => a.Bloquea);
}

public static class CalculoLote
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static ResultadoLote Calcular(EntradaLote e)
    {
        var lote = new Poligono(e.Puntos);
        // El cruce va primero: un lote en forma de moño tiene área neta cero, y el aviso útil es el del orden de los puntos.
        if (lote.Lados >= 3 && lote.SeCruza())
            return Bloqueado(lote, "Los lados del lote se cruzan: revise el orden de los puntos.");
        if (lote.Lados < 3 || lote.Area <= 0.01)
            return Bloqueado(null, "Falta el lote: escriba frente y fondo mayores que cero, o al menos 3 puntos que encierren un área.");

        int frente = ((e.Frente % lote.Lados) + lote.Lados) % lote.Lados;
        IReadOnlyList<TipoLado> tipos = e.Tipos is { } amano && amano.Count == lote.Lados ? amano : LadosLote.Clasificar(lote, frente);
        Desfase d = DesfaseRetiros.Calcular(lote, tipos, e.Retiros);

        double largoFrente = Enumerable.Range(0, lote.Lados).Where(i => tipos[i] == TipoLado.Frente).Sum(lote.LargoLado);
        double areaUtil = d.Util?.Area ?? 0;
        var avisos = new List<Aviso>();
        Ficha f = Validar(e.Ficha, avisos);
        double? ocupacion = f.Imo * lote.Area;
        double? construccion = f.Imc * lote.Area;
        double? posible = f.Pisos is int pisos && d.Util is not null ? areaUtil * pisos : null;
        var resumen = new Resumen(lote.Area, areaUtil, lote.Area - areaUtil, largoFrente, ocupacion, construccion, posible);

        if (d.Problema is { } problema)
            avisos.Insert(0, new Aviso(problema, true));
        if (e.RetirosVacios is { Count: > 0 } vacios)
            avisos.Add(new Aviso($"Faltan los retiros {Lista(vacios.Select(NombreRetiro))} de la ficha: se toman como 0.", false));
        if (f.FrenteMinimo is double fm && largoFrente < fm - Poligono.Tolerancia)
            avisos.Add(new Aviso(Texto($"El frente mide {largoFrente:0.##} m y la ficha pide al menos {fm:0.##} m."), false));
        if (f.LoteMinimo is double lm && lote.Area < lm - 0.01)
            avisos.Add(new Aviso(Texto($"El lote mide {lote.Area:0.##} m² y la ficha pide al menos {lm:0.##} m²."), false));
        if (ocupacion is double om && d.Util is not null && areaUtil > om + 0.01)
            avisos.Add(new Aviso(Texto($"El área útil ({areaUtil:0.##} m²) supera la ocupación que permite el IMO ({om:0.##} m²): manda el índice, no los retiros."), false));
        if (construccion is double cm && posible is double pm && pm > cm + 0.01)
            avisos.Add(new Aviso(Texto($"Con {f.Pisos} pisos se podrían construir {pm:0.##} m², más que los {cm:0.##} m² que permite el IMC."), false));

        return new ResultadoLote(lote, tipos, d.Util, d.Trazos, resumen, avisos);
    }

    /// <summary>Un dato de la ficha en 0 o negativo es un error de escritura: se ignora y se avisa (decisión 17).</summary>
    private static Ficha Validar(Ficha f, List<Aviso> avisos)
    {
        double? Positivo(double? valor, string nombre)
        {
            if (valor is not double v || v > 0)
                return valor;
            avisos.Add(new Aviso($"{nombre} debe ser mayor que 0: no se tuvo en cuenta.", false));
            return null;
        }
        int? pisos = f.Pisos;
        if (pisos is int p && p < 1)
        {
            avisos.Add(new Aviso("Los pisos deben ser 1 o más: no se tuvo en cuenta.", false));
            pisos = null;
        }
        return new Ficha(Positivo(f.Imo, "El IMO"), Positivo(f.Imc, "El IMC"), pisos,
            Positivo(f.LoteMinimo, "El lote mínimo"), Positivo(f.FrenteMinimo, "El frente mínimo"));
    }

    private static string NombreRetiro(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => "frontal",
        TipoLado.Posterior => "posterior",
        TipoLado.LateralIzquierdo => "lateral izquierdo",
        _ => "lateral derecho",
    };

    /// <summary>«a», «a y b», «a, b y c».</summary>
    private static string Lista(IEnumerable<string> partes)
    {
        List<string> l = partes.ToList();
        return l.Count <= 1 ? string.Concat(l) : $"{string.Join(", ", l.Take(l.Count - 1))} y {l[^1]}";
    }

    private static string Texto(FormattableString texto) => texto.ToString(Es);

    private static ResultadoLote Bloqueado(Poligono? lote, string texto) =>
        new(lote, [], null, [], null, [new Aviso(texto, true)]);
}
