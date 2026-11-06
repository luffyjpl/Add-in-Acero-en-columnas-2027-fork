namespace CQEING.Despiece.Calculo;

/// <summary>Un eje de la grilla leído del modelo, en metros.</summary>
public sealed record EjeLeido(string Nombre, PuntoModelo Desde, PuntoModelo Hasta);

/// <summary>Una viga, vigueta o columna del nivel: sus puntos en planta, en metros.</summary>
public sealed record ElementoEnPlanta(string Id, bool EsColumna, IReadOnlyList<PuntoModelo> Puntos);

/// <summary>Un eje ya en el dibujo, en píxeles.</summary>
public sealed record EjeDibujado(string Nombre, double X1, double Y1, double X2, double Y2);

/// <summary>El contorno de un elemento en el dibujo, en píxeles.</summary>
public sealed record ElementoDibujado(string Id, bool EsColumna, IReadOnlyList<PuntoModelo> Contorno);

public sealed record PlantaDibujada(double Ancho, double Alto, IReadOnlyList<EjeDibujado> Ejes, IReadOnlyList<ElementoDibujado> Elementos);

/// <summary>
/// La planta del tablero, dibujada por la página y no exportada de Revit (#285, #286; decisión 7 del plan
/// docs/planes/2026-11-05-reunion-despiece-287.md): como el tablero de vigas, solo se dibuja lo que se lee (ejes,
/// columnas y vigas del nivel), así que nunca sale un CAD, una cota ni una marca.
/// </summary>
public static class ArmadorPlanta
{
    public const double PixelesPorMetro = 60;

    /// <summary>Lo que se deja alrededor de los elementos.</summary>
    public const double Margen = 2.0;

    /// <summary>
    /// El dibujo de un nivel. El recuadro abarca los elementos con <see cref="Margen"/>; los ejes se recortan a él y se
    /// omiten los que no lo cruzan. Sin elementos, el recuadro es el de los ejes. La Y del dibujo va hacia abajo.
    /// </summary>
    public static PlantaDibujada Armar(IEnumerable<EjeLeido> ejes, IEnumerable<ElementoEnPlanta> elementos)
    {
        List<ElementoEnPlanta> conPuntos = elementos.Where(e => e.Puntos.Count > 0).ToList();
        List<EjeLeido> listaEjes = ejes.ToList();
        List<PuntoModelo> todos = conPuntos.SelectMany(e => e.Puntos).ToList();
        if (todos.Count == 0)
            todos = listaEjes.SelectMany(e => new[] { e.Desde, e.Hasta }).ToList();
        if (todos.Count == 0)
            return new PlantaDibujada(0, 0, [], []);

        double x0 = todos.Min(p => p.X) - Margen, x1 = todos.Max(p => p.X) + Margen;
        double y0 = todos.Min(p => p.Y) - Margen, y1 = todos.Max(p => p.Y) + Margen;
        PuntoModelo APixel(PuntoModelo p) => new((p.X - x0) * PixelesPorMetro, (y1 - p.Y) * PixelesPorMetro);

        var dibujados = new List<EjeDibujado>();
        foreach (EjeLeido eje in listaEjes)
        {
            if (Recortar(eje.Desde, eje.Hasta, x0, y0, x1, y1) is not { } r)
                continue;
            PuntoModelo a = APixel(r.A), b = APixel(r.B);
            dibujados.Add(new EjeDibujado(eje.Nombre, a.X, a.Y, b.X, b.Y));
        }
        List<ElementoDibujado> contornos = conPuntos
            .Select(e => new ElementoDibujado(e.Id, e.EsColumna, Envolvente(e.Puntos).Select(APixel).ToList()))
            .ToList();
        return new PlantaDibujada((x1 - x0) * PixelesPorMetro, (y1 - y0) * PixelesPorMetro, dibujados, contornos);
    }

    /// <summary>
    /// La envolvente convexa (cadena monótona), sin puntos repetidos ni alineados. Una viga recta o una columna
    /// rectangular o circular quedan con su contorno real en planta.
    /// </summary>
    public static IReadOnlyList<PuntoModelo> Envolvente(IEnumerable<PuntoModelo> puntos)
    {
        List<PuntoModelo> p = puntos.Select(q => new PuntoModelo(Math.Round(q.X, 4), Math.Round(q.Y, 4))).Distinct()
            .OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3)
            return p;
        static double Cruz(PuntoModelo o, PuntoModelo a, PuntoModelo b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var casco = new List<PuntoModelo>();
        foreach (IEnumerable<PuntoModelo> recorrido in new[] { p, Enumerable.Reverse(p) })
        {
            int inicio = casco.Count;
            foreach (PuntoModelo q in recorrido)
            {
                while (casco.Count >= inicio + 2 && Cruz(casco[^2], casco[^1], q) <= 1e-9)
                    casco.RemoveAt(casco.Count - 1);
                casco.Add(q);
            }
            casco.RemoveAt(casco.Count - 1);
        }
        return casco;
    }

    /// <summary>El tramo del segmento dentro del recuadro (Liang-Barsky); null si no lo cruza.</summary>
    private static (PuntoModelo A, PuntoModelo B)? Recortar(PuntoModelo a, PuntoModelo b, double x0, double y0, double x1, double y1)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, t0 = 0, t1 = 1;
        foreach ((double p, double q) in new[] { (-dx, a.X - x0), (dx, x1 - a.X), (-dy, a.Y - y0), (dy, y1 - a.Y) })
        {
            if (Math.Abs(p) < 1e-12)
            {
                if (q < 0)
                    return null;
                continue;
            }
            double t = q / p;
            if (p < 0)
                t0 = Math.Max(t0, t);
            else
                t1 = Math.Min(t1, t);
            if (t0 > t1)
                return null;
        }
        return (new PuntoModelo(a.X + t0 * dx, a.Y + t0 * dy), new PuntoModelo(a.X + t1 * dx, a.Y + t1 * dy));
    }
}
