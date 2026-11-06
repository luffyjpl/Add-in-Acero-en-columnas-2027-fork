namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Varillas en su lugar en la sección (#280): plan docs/planes/2026-11-05-reunion-despiece-287.md, decisión 35.</summary>
public class PosicionesIdealesTests
{
    private static PuntoModelo P(double x, double y) => new(x, y);

    /// <summary>Columna de 0.30 con 3 x 3 varillas en ±0.10; en cada una, una pieza en su lugar y la otra corrida.</summary>
    [Fact]
    public void CadaVarillaVuelveASuLugarAunqueSeHayaCorridoALoLargoDeLaCara()
    {
        var varillas = new List<IReadOnlyList<PuntoModelo>>
        {
            new[] { P(-0.10, -0.10), P(-0.08, -0.08) },  // esquina, corrida hacia adentro
            new[] { P(0.08, 0.10), P(0.10, 0.10) },      // esquina, corrida a lo largo de la cara (la de abajo es la corrida)
            new[] { P(0.10, -0.10), P(0.10, -0.08) },    // esquina
            new[] { P(-0.10, 0.10), P(-0.10, 0.08) },    // esquina
            new[] { P(0.00, 0.10), P(0.02, 0.10) },      // cara de arriba, corrida a lo largo
            new[] { P(0.00, -0.08), P(0.00, -0.10) },    // cara de abajo, corrida hacia adentro
            new[] { P(0.10, 0.00), P(0.08, 0.00) },      // cara derecha
            new[] { P(-0.10, 0.02), P(-0.10, 0.00) },    // cara izquierda, corrida a lo largo
        };

        IReadOnlyList<PuntoModelo>? ideal = PosicionesIdeales.Calcular(varillas, P(0, 0));

        Assert.NotNull(ideal);
        PuntoModelo[] esperado = [P(-0.10, -0.10), P(0.10, 0.10), P(0.10, -0.10), P(-0.10, 0.10), P(0, 0.10), P(0, -0.10), P(0.10, 0), P(-0.10, 0)];
        for (int i = 0; i < esperado.Length; i++)
        {
            Assert.Equal(esperado[i].X, ideal![i].X, 6);
            Assert.Equal(esperado[i].Y, ideal[i].Y, 6);
        }
    }

    /// <summary>La 5-7/5-8 del 06-nov: en la esquina de arriba a la derecha las dos piezas están corridas.</summary>
    [Fact]
    public void UnaEsquinaConLasDosPiezasCorridasVaALaEsquinaDeSuLado()
    {
        var varillas = new List<IReadOnlyList<PuntoModelo>>
        {
            new[] { P(3.412, 7.931), P(3.412, 7.912) },  // abajo a la izquierda
            new[] { P(3.588, 7.931), P(3.588, 7.912) },  // abajo a la derecha
            new[] { P(3.570, 8.088), P(3.550, 8.088) },  // arriba a la derecha: las dos corridas
            new[] { P(3.431, 8.088), P(3.412, 8.088) },  // arriba a la izquierda
            new[] { P(3.470, 8.093), P(3.454, 8.093) },  // cara de arriba
            new[] { P(3.530, 8.093), P(3.514, 8.093) },  // cara de arriba
        };

        IReadOnlyList<PuntoModelo>? ideal = PosicionesIdeales.Calcular(varillas, P(3.50, 8.00));

        Assert.NotNull(ideal);
        Assert.Equal(3.588, ideal![2].X, 6);
        Assert.Equal(8.088, ideal[2].Y, 6);
        Assert.Equal(3.412 + 0.176 / 3, ideal[4].X, 6);
        Assert.Equal(3.412 + 2 * 0.176 / 3, ideal[5].X, 6);
    }

    [Fact]
    public void ConMenosDeCuatroVarillasNoHayLugar() =>
        Assert.Null(PosicionesIdeales.Calcular([new[] { P(0, 0) }, new[] { P(1, 0) }, new[] { P(0, 1) }], P(0.5, 0.5)));
}
