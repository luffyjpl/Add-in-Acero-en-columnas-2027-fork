namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Escala elegida en el tablero (#272): plan docs/planes/2026-11-05-reunion-despiece-287.md, paso 4.</summary>
public class EscalaVistaTests
{
    [Fact]
    public void PorDefectoEsUnoCincuenta() => Assert.Equal(50, EscalaVista.PorDefecto);

    [Fact]
    public void LaSeccionPorDefectoEsUnoVeinte() => Assert.Equal(20, EscalaVista.PorDefectoSeccion);

    [Theory]
    [InlineData(50, 50)]
    [InlineData(37.4, 37)]
    [InlineData(37.5, 38)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100000, 24000)]
    [InlineData(double.NaN, 50)]
    public void SeRedondeaYSeLimita(double pedida, int esperada) => Assert.Equal(esperada, EscalaVista.Limitar(pedida));

    [Fact]
    public void ElFactorEsRespectoDeUnoQuince()
    {
        Assert.Equal(1, EscalaVista.Factor(15), 9);
        Assert.Equal(50.0 / 15, EscalaVista.Factor(50), 9);
    }

    [Fact]
    public void LaListaTraeLaDeAntesYLaDePorDefecto()
    {
        Assert.Contains(15, EscalaVista.Lista);
        Assert.Contains(EscalaVista.PorDefecto, EscalaVista.Lista);
        Assert.Equal(EscalaVista.Lista.OrderBy(e => e), EscalaVista.Lista);
    }
}
