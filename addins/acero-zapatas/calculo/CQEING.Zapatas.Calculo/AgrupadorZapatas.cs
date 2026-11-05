namespace CQEING.Zapatas.Calculo;

/// <summary>Una zapata para comparar: su id, su contorno en sus propios ejes y su altura, en mm.</summary>
public sealed record ZapataParaAgrupar(string Id, IReadOnlyList<Punto> Contorno, double Altura);

/// <summary>
/// Zapatas iguales (#253): mismo contorno, en forma y medidas, y misma altura, sin importar dónde estén ni desde qué
/// vértice se haya leído el contorno. Tolerancia de 1 mm.
/// </summary>
public static class AgrupadorZapatas
{
    private const double Tolerancia = 1;

    public static IReadOnlyList<IReadOnlyList<string>> Agrupar(IEnumerable<ZapataParaAgrupar> zapatas)
    {
        var grupos = new List<(ZapataParaAgrupar Modelo, List<string> Ids)>();
        foreach (ZapataParaAgrupar z in zapatas)
        {
            int i = grupos.FindIndex(g => SonIguales(g.Modelo, z));
            if (i >= 0)
                grupos[i].Ids.Add(z.Id);
            else
                grupos.Add((z, [z.Id]));
        }
        return grupos.Select(g => (IReadOnlyList<string>)g.Ids).ToList();
    }

    public static bool SonIguales(ZapataParaAgrupar a, ZapataParaAgrupar b)
    {
        if (Math.Abs(a.Altura - b.Altura) > Tolerancia || a.Contorno.Count != b.Contorno.Count)
            return false;
        List<Punto> p = AlOrigen(a.Contorno), q = AlOrigen(b.Contorno);
        int n = p.Count;
        // El mismo contorno puede empezar en otro vértice o ir en el otro sentido.
        List<Punto> inverso = Enumerable.Reverse(q).ToList();
        return Enumerable.Range(0, n).Any(desde => Coincide(p, q, desde) || Coincide(p, inverso, desde));
    }

    private static bool Coincide(List<Punto> p, List<Punto> q, int desde) =>
        p.Select((v, i) => (v, w: q[(i + desde) % q.Count])).All(x => Math.Abs(x.v.X - x.w.X) <= Tolerancia && Math.Abs(x.v.Y - x.w.Y) <= Tolerancia);

    private static List<Punto> AlOrigen(IReadOnlyList<Punto> c)
    {
        double x = c.Min(v => v.X), y = c.Min(v => v.Y);
        return c.Select(v => new Punto(v.X - x, v.Y - y)).ToList();
    }
}
