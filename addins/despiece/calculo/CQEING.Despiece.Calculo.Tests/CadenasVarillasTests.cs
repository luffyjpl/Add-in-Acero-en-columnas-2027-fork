namespace CQEING.Despiece.Calculo.Tests;

/// <summary>
/// Qué piezas forman cada varilla (hallazgo 31 del plan de columnas). Medidas en metros, de la columna 781574 de
/// "Prueba 08072026": circular, 12 varillas a ~5 cm una de otra; la pieza de arriba corrida 1.5 cm por el traslapo.
/// </summary>
public class CadenasVarillasTests
{
    private const double Radio = 0.094;

    /// <summary>Las 12 de abajo y, después, las 12 de arriba corridas a un lado. A traslapa a 0.79–2.29 y B a 4.74–5.84.</summary>
    private static List<PiezaEnPlanta> Circular()
    {
        var abajo = new List<PiezaEnPlanta>();
        var arriba = new List<PiezaEnPlanta>();
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6, x = 3.5 + Radio * Math.Cos(a), y = 8.0 + Radio * Math.Sin(a);
            bool esA = i % 2 == 0;
            abajo.Add(new PiezaEnPlanta(x, y, -0.92, esA ? 2.29 : 5.84));
            // Corrida 1.5 cm en el sentido del anillo: queda a ~3.5 cm de la vecina.
            arriba.Add(new PiezaEnPlanta(x - 0.015 * Math.Sin(a), y + 0.015 * Math.Cos(a), esA ? 0.79 : 4.74, 7.00));
        }
        return [.. abajo, .. arriba];
    }

    [Fact]
    public void EnLaColumnaCircularCadaVarillaTieneSusDosPiezas()
    {
        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir(Circular());

        Assert.Equal(12, cadenas.Count);
        Assert.All(cadenas, c => Assert.Equal([c[0], c[0] + 12], c));
    }

    [Fact]
    public void ElOrdenDeEntradaNoImporta()
    {
        List<PiezaEnPlanta> piezas = Circular();
        piezas.Reverse();

        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir(piezas);

        Assert.Equal(12, cadenas.Count);
        Assert.All(cadenas, c => Assert.Equal(2, c.Count));
        // De abajo hacia arriba.
        Assert.All(cadenas, c => Assert.True(piezas[c[0]].Desde < piezas[c[1]].Desde));
    }

    [Fact]
    public void TresPiezasSeguidasSonUnaVarilla()
    {
        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir(
        [
            new(0, 0, 0, 3.5), new(0.013, 0, 2.5, 6.5), new(0, 0, 5.5, 9.0),
        ]);

        Assert.Equal([0, 1, 2], Assert.Single(cadenas));
    }

    [Fact]
    public void UnaPiezaQueNoSubeMasArribaNoEsTraslapo()
    {
        // Dos barras en paquete, de la misma altura: son dos varillas, no una.
        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir([new(0, 0, 0, 7.0), new(0.019, 0, 0, 7.0), new(0, 0.01, 1.0, 3.0)]);

        Assert.Equal(3, cadenas.Count);
    }

    [Fact]
    public void UnaPiezaLejosNoSeUne()
    {
        IReadOnlyList<IReadOnlyList<int>> cadenas = CadenasVarillas.Unir([new(0, 0, 0, 3.5), new(0.30, 0, 2.5, 7.0)]);

        Assert.Equal(2, cadenas.Count);
    }
}
