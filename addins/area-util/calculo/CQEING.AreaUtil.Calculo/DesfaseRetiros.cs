namespace CQEING.AreaUtil.Calculo;

/// <summary>Las claves de los estilos de línea (decisiones 1, 2 y 11 del plan). Revit les pone el nombre «CQ …».</summary>
public static class Estilos
{
    public const string Lote = "lote";
    public const string RetiroFrontal = "retiro-frontal";
    public const string RetiroLateral = "retiro-lateral";
    public const string RetiroPosterior = "retiro-posterior";
    public const string AreaUtil = "area-util";

    public static string DeRetiro(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => RetiroFrontal,
        TipoLado.Posterior => RetiroPosterior,
        _ => RetiroLateral,
    };
}

/// <summary>Un tramo recto que se dibuja en Revit con el estilo <paramref name="Estilo"/>.</summary>
public sealed record Trazo(Punto A, Punto B, string Estilo);

/// <summary>El área útil y lo que se dibuja; si los retiros no dejan área válida, <see cref="Problema"/> dice por qué.</summary>
public sealed record Desfase(Poligono? Util, IReadOnlyList<Trazo> Trazos, string? Problema);

/// <summary>
/// Decisión 6 del spec: cada lado se corre hacia adentro su retiro y cada esquina del área útil es el corte de dos lados
/// vecinos ya corridos. Decisión 10 del plan: los tramos de retiro van del lado vecino del lote a la esquina del área útil.
/// </summary>
public static class DesfaseRetiros
{
    public static Desfase Calcular(Poligono lote, IReadOnlyList<TipoLado> tipos, Retiros retiros)
    {
        int n = lote.Lados;
        var origen = new Punto[n];
        var direccion = new Punto[n];
        var distancia = new double[n];
        for (int i = 0; i < n; i++)
        {
            distancia[i] = retiros.Para(tipos[i]);
            if (distancia[i] < 0)
                return Fallo("Los retiros no pueden ser negativos.");
            (Punto a, Punto b) = lote.Lado(i);
            direccion[i] = (b - a).Unitario();
            origen[i] = a + lote.NormalAdentro(i) * distancia[i];
        }

        var esquinas = new Punto[n];
        for (int j = 0; j < n; j++)
        {
            int previo = (j - 1 + n) % n;
            if (!Rectas.Interseccion(origen[previo], direccion[previo], origen[j], direccion[j], out esquinas[j]))
                return Fallo($"Los lados {previo + 1} y {j + 1} son paralelos y seguidos: revise los puntos del lote.");
        }

        // Si un lado del área útil quedó al revés, los retiros de sus vecinos se lo comieron.
        for (int i = 0; i < n; i++)
            if ((esquinas[(i + 1) % n] - esquinas[i]).Escalar(direccion[i]) <= Poligono.Tolerancia)
                return Fallo($"Los retiros se comen el lado {i + 1} ({Nombre(tipos[i])}): no queda área útil.");

        var util = new Poligono(esquinas);
        if (util.Lados < 3 || util.Area <= 0.01 || util.EsAntihorario != lote.EsAntihorario || util.SeCruza())
            return Fallo("Los retiros no dejan un área útil válida.");
        if (esquinas.Any(e => !lote.ContieneOBorde(e)))
            return Fallo("El área útil se sale del lote: revise los retiros.");

        var trazos = new List<Trazo>();
        for (int i = 0; i < n; i++)
            trazos.Add(new Trazo(lote.Lado(i).A, lote.Lado(i).B, Estilos.Lote));
        for (int i = 0; i < n; i++)
            trazos.Add(new Trazo(esquinas[i], esquinas[(i + 1) % n], Estilos.AreaUtil));
        for (int i = 0; i < n; i++)
        {
            if (distancia[i] <= Poligono.Tolerancia)
                continue;
            int previo = (i - 1 + n) % n, siguiente = (i + 1) % n;
            string estilo = Estilos.DeRetiro(tipos[i]);
            double T(Punto p) => (p - origen[i]).Escalar(direccion[i]);
            if (Rectas.Interseccion(origen[i], direccion[i], lote.Lado(previo).A, direccion[previo], out Punto inicio)
                && T(esquinas[i]) - T(inicio) > Poligono.Tolerancia)
                trazos.Add(new Trazo(inicio, esquinas[i], estilo));
            if (Rectas.Interseccion(origen[i], direccion[i], lote.Lado(siguiente).A, direccion[siguiente], out Punto fin)
                && T(fin) - T(esquinas[siguiente]) > Poligono.Tolerancia)
                trazos.Add(new Trazo(esquinas[siguiente], fin, estilo));
        }
        return new Desfase(util, trazos, null);
    }

    public static string Nombre(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => "frente",
        TipoLado.Posterior => "posterior",
        TipoLado.LateralIzquierdo => "lateral izquierdo",
        _ => "lateral derecho",
    };

    private static Desfase Fallo(string problema) => new(null, [], problema);
}
