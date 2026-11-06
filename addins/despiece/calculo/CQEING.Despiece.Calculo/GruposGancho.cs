namespace CQEING.Despiece.Calculo;

/// <summary>
/// Hacia dónde mira el gancho de una barra sacada en la vista (#277): a la derecha, a la izquierda, o ninguno de lado
/// (sin gancho, o con el gancho hacia quien mira).
/// </summary>
public enum SentidoGancho
{
    Ninguno,
    Derecha,
    Izquierda,
}

/// <summary>
/// El orden de las barras sacadas de la columna (#277, Carlos en la reunión del 05-nov): primero, juntas, todas las que
/// tienen el gancho a la derecha; un hueco; después las del gancho a la izquierda. Así los ganchos quedan en espejo,
/// mirándose, y quien corta ve de un vistazo cuántas son de cada largo.
/// </summary>
public static class GruposGancho
{
    /// <summary>
    /// Hacia dónde se dibuja el gancho de una varilla según su grupo de traslapo (decisión 18 del plan
    /// docs/planes/2026-11-05-reunion-despiece-287.md, Carlos): el A a la izquierda y el B a la derecha. El dibujo es
    /// conceptual: dice qué barras traslapan en el mismo punto. Sin grupo, no se impone ningún lado.
    /// </summary>
    public static SentidoGancho DeGrupo(GrupoTraslapo? grupo) => grupo switch
    {
        GrupoTraslapo.A => SentidoGancho.Izquierda,
        GrupoTraslapo.B => SentidoGancho.Derecha,
        _ => SentidoGancho.Ninguno,
    };

    /// <summary>
    /// Los índices de cada grupo, en el orden en que llegaron y en el orden en que se dibujan: las del gancho a la
    /// derecha, las que no tienen gancho de lado y las del gancho a la izquierda. Las sin gancho de lado van aparte, en el
    /// medio: mezcladas con las de la derecha quedaban entre dos del mismo lado (decisión 24 del plan de #287).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Separar(IReadOnlyList<SentidoGancho> sentidos)
    {
        List<int> todos = Enumerable.Range(0, sentidos.Count).ToList();
        return new[] { SentidoGancho.Derecha, SentidoGancho.Ninguno, SentidoGancho.Izquierda }
            .Select(lado => (IReadOnlyList<int>)todos.Where(i => sentidos[i] == lado).ToList()).ToList();
    }

    /// <summary>
    /// El sentido de una línea de piezas (las de una misma varilla): el de la primera pieza con un gancho de lado.
    /// <paramref name="direcciones"/>: de cada gancho, cuánto va a la derecha de la vista su punta (de -1 a 1).
    /// </summary>
    public static SentidoGancho DeLinea(IEnumerable<double> direcciones)
    {
        foreach (double d in direcciones)
        {
            if (d > 0.5)
                return SentidoGancho.Derecha;
            if (d < -0.5)
                return SentidoGancho.Izquierda;
        }
        return SentidoGancho.Ninguno;
    }
}
