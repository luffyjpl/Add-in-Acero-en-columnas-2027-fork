namespace CQEING.Despiece.Calculo;

/// <summary>
/// Dónde va cada varilla de una columna rectangular "en su lugar", sin el corrimiento del traslapo (#280, decisión 35 del
/// plan docs/planes/2026-11-05-reunion-despiece-287.md). En cada traslapo una pieza se corre para no chocar con la otra,
/// hacia adentro o a lo largo de la cara, y el modelo no guarda cuál quedó en su lugar. Se reconstruye con la regla del
/// add-in de columnas (<c>SeccionPlanta.PosicionIntermedia</c>): las esquinas tocan las dos caras del estribo y las
/// barras de cada cara se reparten en partes iguales entre sus dos esquinas.
/// </summary>
public static class PosicionesIdeales
{
    /// <summary>
    /// <paramref name="varillas"/>: las piezas de cada varilla en planta (en cualquier unidad, la misma para todo).
    /// Devuelve el lugar de cada varilla, en el mismo orden; null si no se reconocen cuatro esquinas.
    /// </summary>
    public static IReadOnlyList<PuntoModelo>? Calcular(IReadOnlyList<IReadOnlyList<PuntoModelo>> varillas, PuntoModelo centro)
    {
        if (varillas.Count < 4 || varillas.Any(v => v.Count == 0))
            return null;
        PuntoModelo Medio(IReadOnlyList<PuntoModelo> v) => new(v.Average(p => p.X), v.Average(p => p.Y));
        List<PuntoModelo> medios = varillas.Select(Medio).ToList();

        // Cada esquina: la varilla más metida en su cuadrante. Su lugar, en cada eje, lo más afuera de su lado entre las
        // piezas de las dos esquinas de ese lado: a veces las dos piezas de una esquina están corridas (decisión 40).
        var esquinas = new Dictionary<(int, int), int>();
        foreach ((int sx, int sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            double Hacia(PuntoModelo p) => sx * (p.X - centro.X) + sy * (p.Y - centro.Y);
            int i = Enumerable.Range(0, varillas.Count).OrderByDescending(k => Hacia(medios[k])).First();
            if (esquinas.ContainsValue(i))
                return null;
            esquinas[(sx, sy)] = i;
        }
        var ideal = new PuntoModelo[varillas.Count];
        double LadoX(int sx) => sx > 0
            ? varillas[esquinas[(sx, -1)]].Concat(varillas[esquinas[(sx, 1)]]).Max(p => p.X)
            : varillas[esquinas[(sx, -1)]].Concat(varillas[esquinas[(sx, 1)]]).Min(p => p.X);
        double LadoY(int sy) => sy > 0
            ? varillas[esquinas[(-1, sy)]].Concat(varillas[esquinas[(1, sy)]]).Max(p => p.Y)
            : varillas[esquinas[(-1, sy)]].Concat(varillas[esquinas[(1, sy)]]).Min(p => p.Y);
        foreach (((int sx, int sy), int i) in esquinas)
            ideal[i] = new PuntoModelo(LadoX(sx), LadoY(sy));

        PuntoModelo Esq(int sx, int sy) => ideal[esquinas[(sx, sy)]];
        double medioX = (Esq(1, 1).X - Esq(-1, 1).X + Esq(1, -1).X - Esq(-1, -1).X) / 4;
        double medioY = (Esq(1, 1).Y - Esq(1, -1).Y + Esq(-1, 1).Y - Esq(-1, -1).Y) / 4;
        if (medioX <= 0 || medioY <= 0)
            return null;

        // Las demás, por cara: la de la cara más cercana (relativa al tamaño). Lo perpendicular, la pieza más afuera
        // (cada barra se separa de su cara según su diámetro); lo de a lo largo, repartido entre las dos esquinas.
        List<int> resto = Enumerable.Range(0, varillas.Count).Where(i => !esquinas.ContainsValue(i)).ToList();
        foreach ((int sx, int sy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            List<int> cara = resto.Where(i =>
            {
                double dx = (medios[i].X - centro.X) / medioX, dy = (medios[i].Y - centro.Y) / medioY;
                return sx == 0 ? Math.Abs(dy) >= Math.Abs(dx) && Math.Sign(dy) == sy : Math.Abs(dx) > Math.Abs(dy) && Math.Sign(dx) == sx;
            }).ToList();
            if (sx == 0)
            {
                PuntoModelo a = Esq(-1, sy), b = Esq(1, sy);
                List<int> orden = cara.OrderBy(i => medios[i].X).ToList();
                for (int k = 0; k < orden.Count; k++)
                {
                    double y = sy > 0 ? varillas[orden[k]].Max(p => p.Y) : varillas[orden[k]].Min(p => p.Y);
                    ideal[orden[k]] = new PuntoModelo(a.X + (k + 1) * (b.X - a.X) / (orden.Count + 1), y);
                }
            }
            else
            {
                PuntoModelo a = Esq(sx, -1), b = Esq(sx, 1);
                List<int> orden = cara.OrderBy(i => medios[i].Y).ToList();
                for (int k = 0; k < orden.Count; k++)
                {
                    double x = sx > 0 ? varillas[orden[k]].Max(p => p.X) : varillas[orden[k]].Min(p => p.X);
                    ideal[orden[k]] = new PuntoModelo(x, a.Y + (k + 1) * (b.Y - a.Y) / (orden.Count + 1));
                }
            }
        }
        return ideal.Any(p => p is null) ? null : ideal;
    }
}
