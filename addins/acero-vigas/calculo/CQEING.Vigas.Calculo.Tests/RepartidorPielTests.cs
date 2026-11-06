using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Seccion;

namespace CQEING.Vigas.Calculo.Tests;

/// <summary>
/// Piel en el corte (06-nov): viga 250 × 500, recubrimiento 40, estribo #3 (9.5 mm), 3#5 arriba y abajo.
/// La piel va pegada al estribo por dentro y repartida a igual distancia entre las filas de abajo y de arriba.
/// </summary>
public class RepartidorPielTests
{
    private const double Precision = 1e-6;
    private static readonly Reglas Reglas = Reglas.Generales;

    private static EntradaSeccion Viga() =>
        new(Ancho: 250, Alto: 500, Recubrimientos.Iguales(40), DiametroEstribo: 3,
            Superior: [new GrupoBarras(3, 5)], Inferior: [new GrupoBarras(3, 5)], Reglas);

    private static UbicacionPiel Ubicar(GrupoBarras piel, double altoMinimo = 500)
    {
        EntradaSeccion e = Viga();
        return RepartidorPiel.Ubicar(e, RepartidorFilas.Repartir(e), piel, altoMinimo);
    }

    private static (double Abajo, double Arriba) Filas()
    {
        ResultadoSeccion s = RepartidorFilas.Repartir(Viga());
        return (s.Inferior.Filas.SelectMany(f => f).Max(b => b.Y), s.Superior.Filas.SelectMany(f => f).Min(b => b.Y));
    }

    [Fact]
    public void DosPorLado_AIgualDistanciaEntreArribaYAbajo()
    {
        (double abajo, double arriba) = Filas();
        UbicacionPiel u = Ubicar(new GrupoBarras(2, 4));

        Assert.Equal(2, u.Alturas.Count);
        for (int k = 1; k <= 2; k++)
            Assert.Equal(abajo + k * (arriba - abajo) / 3, u.Alturas[k - 1].Izquierda.Y, Precision);
    }

    [Fact]
    public void PegadaAlEstriboPorDentro()
    {
        double x = 40 + Reglas.PorDiametro[3].Diametro + Reglas.PorDiametro[4].Diametro / 2;
        PielEnSeccion p = Ubicar(new GrupoBarras(2, 4)).Alturas[0];

        Assert.Equal(x, p.Izquierda.X, Precision);
        Assert.Equal(250 - x, p.Derecha.X, Precision);
        Assert.Equal(p.Izquierda.Y, p.Derecha.Y, Precision);
        Assert.Equal(1, p.Altura);
    }

    [Fact]
    public void CabenPorLado_ConLaDistanciaLibre()
    {
        (double abajo, double arriba) = Filas();
        double paso = Reglas.PorDiametro[4].Diametro + Reglas.DistanciaLibre;
        int esperado = 0;
        while ((arriba - abajo) / (esperado + 2) >= paso) esperado++;

        UbicacionPiel u = Ubicar(new GrupoBarras(2, 4));

        Assert.Equal(esperado, u.CabenPorLado);
        Assert.True((arriba - abajo) / (u.CabenPorLado + 1) >= paso);
        Assert.True((arriba - abajo) / (u.CabenPorLado + 2) < paso);
    }

    [Fact]
    public void MasDeLasQueCaben_SeRecorta()
    {
        UbicacionPiel u = Ubicar(new GrupoBarras(12, 4));

        Assert.True(u.CabenPorLado < 12);
        Assert.Equal(u.CabenPorLado, u.Alturas.Count);
    }

    [Fact]
    public void ConTramoMasBajo_UsaElMasBajo()
    {
        UbicacionPiel entero = Ubicar(new GrupoBarras(1, 4));
        UbicacionPiel bajo = Ubicar(new GrupoBarras(1, 4), altoMinimo: 400);
        (double abajo, double arriba) = Filas();

        Assert.Equal(abajo + (arriba - abajo) / 2, entero.Alturas[0].Izquierda.Y, Precision);
        Assert.Equal(abajo + (arriba - 100 - abajo) / 2, bajo.Alturas[0].Izquierda.Y, Precision);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(1, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(1, 1, false)]
    public void GanchoArriba(int altura, int alturas, bool esperado) =>
        Assert.Equal(esperado, RepartidorPiel.GanchoArriba(altura, alturas));

    [Fact]
    public void ConErrorEnUnaCara_NoHayPiel()
    {
        EntradaSeccion e = Viga() with { Inferior = [new GrupoBarras(1, 5)] };
        UbicacionPiel u = RepartidorPiel.Ubicar(e, RepartidorFilas.Repartir(e), new GrupoBarras(2, 4), 500);

        Assert.Empty(u.Alturas);
        Assert.Equal(0, u.CabenPorLado);
    }
}
