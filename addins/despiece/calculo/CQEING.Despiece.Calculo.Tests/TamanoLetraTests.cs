namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Tamaño de letra elegido en el tablero (#261): plan docs/planes/2026-11-04-ajustes-despiece-266.md, paso 2.</summary>
public class TamanoLetraTests
{
    [Fact]
    public void PorDefectoEsDosYMedio() => Assert.Equal(2.5, TamanoLetra.PorDefecto);

    [Theory]
    [InlineData(3.5, 3.5)]
    [InlineData(3.7, 3.5)]
    [InlineData(3.8, 4.0)]
    [InlineData(1.0, 2.0)]
    [InlineData(9.0, 5.0)]
    [InlineData(double.NaN, 2.5)]
    public void SeRedondeaAMedioMilimetroYDentroDeLosLimites(double pedido, double esperado) =>
        Assert.Equal(esperado, TamanoLetra.Limitar(pedido));

    [Fact]
    public void LasSeparacionesSeMidieronConDosYMedio()
    {
        Assert.Equal(1.0, TamanoLetra.Factor(2.5), 9);
        Assert.Equal(1.4, TamanoLetra.Factor(3.5), 9);
    }

    [Fact]
    public void ElTipoLlevaElTamanoEnElNombre()
    {
        // Un tipo por tamaño: crear vistas con otro tamaño no cambia las ya creadas (decisión 4).
        Assert.Equal("CQ Despiece etiqueta 3.5 mm", TamanoLetra.Nombre("CQ Despiece etiqueta", 3.5));
        Assert.Equal("CQ Despiece etiqueta 2.0 mm", TamanoLetra.Nombre("CQ Despiece etiqueta", 2));
    }
}
