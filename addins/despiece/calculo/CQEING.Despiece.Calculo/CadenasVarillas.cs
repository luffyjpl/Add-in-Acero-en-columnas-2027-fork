namespace CQEING.Despiece.Calculo;

/// <summary>Una pieza vertical de varilla: su eje en planta y de qué altura a qué altura va, en metros.</summary>
public record PiezaEnPlanta(double X, double Y, double Desde, double Hasta);

/// <summary>
/// Qué piezas forman cada varilla de una columna (hallazgo 31 del plan de columnas). No basta la cercanía en planta: la
/// pieza de arriba va corrida por el traslapo, y en una columna circular eso la deja cerca de la varilla vecina.
/// </summary>
public static class CadenasVarillas
{
    /// <summary>
    /// Una pieza sigue a otra si traslapa con ella: empieza dentro de ella y termina más arriba, a no más de
    /// <paramref name="corrimiento"/> en planta. Se emparejan primero las más cercanas, y cada pieza tiene a lo sumo una
    /// abajo y una arriba. Devuelve los índices de cada varilla, de abajo hacia arriba.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Unir(IReadOnlyList<PiezaEnPlanta> piezas, double corrimiento = 0.05, double tolerancia = 0.01)
    {
        var pares = new List<(int Abajo, int Arriba, double Distancia)>();
        for (int p = 0; p < piezas.Count; p++)
        {
            for (int q = 0; q < piezas.Count; q++)
            {
                PiezaEnPlanta a = piezas[p], b = piezas[q];
                double distancia = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
                if (p != q && b.Desde > a.Desde + tolerancia && b.Desde < a.Hasta - tolerancia && b.Hasta > a.Hasta + tolerancia && distancia <= corrimiento)
                    pares.Add((p, q, distancia));
            }
        }

        var siguiente = new Dictionary<int, int>();
        var anterior = new HashSet<int>();
        foreach ((int abajo, int arriba, _) in pares.OrderBy(x => x.Distancia))
        {
            if (siguiente.ContainsKey(abajo) || anterior.Contains(arriba))
                continue;
            siguiente[abajo] = arriba;
            anterior.Add(arriba);
        }

        var cadenas = new List<IReadOnlyList<int>>();
        foreach (int inicio in Enumerable.Range(0, piezas.Count).Where(i => !anterior.Contains(i)).OrderBy(i => piezas[i].Desde))
        {
            var cadena = new List<int> { inicio };
            while (siguiente.TryGetValue(cadena[^1], out int n))
                cadena.Add(n);
            cadenas.Add(cadena);
        }
        return cadenas;
    }
}
