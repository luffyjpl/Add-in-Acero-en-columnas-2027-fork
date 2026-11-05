namespace CQEING.Despiece.Calculo.Tests;

/// <summary>En la columna la barra se mueve a los lados (decisión 14); lo de vigas sigue igual.</summary>
public class MovimientoTests
{
    [Fact]
    public void SinDxLaBarraNoSeMueveALosLados()
    {
        var m = new Movimiento(0.2, 0.1, 0.0);

        Assert.Equal(0, m.Dx);
        Assert.Equal(0.2, m.Dz);
    }

    [Fact]
    public void ConDxSeGuardaAparte()
    {
        var m = new Movimiento(0, 0.1, 0.05, Dx: 0.3);

        Assert.Equal(0.3, m.Dx);
        Assert.Equal(0.1, m.Tx);
    }
}
