namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Barras sacadas agrupadas por el gancho (#277): plan docs/planes/2026-11-05-reunion-despiece-287.md, paso 10.</summary>
public class GruposGanchoTests
{
    [Fact]
    public void PrimeroLasDeLaDerechaYDespuesLasDeLaIzquierda()
    {
        var sentidos = new[] { SentidoGancho.Izquierda, SentidoGancho.Derecha, SentidoGancho.Izquierda, SentidoGancho.Derecha };

        IReadOnlyList<IReadOnlyList<int>> grupos = GruposGancho.Separar(sentidos);

        Assert.Equal([1, 3], grupos[0]);
        Assert.Empty(grupos[1]);
        Assert.Equal([0, 2], grupos[2]);
    }

    [Fact]
    public void LasSinGanchoDeLadoVanEnElMedioYNoEntreLasDeLaDerecha()
    {
        // La prueba del 06-nov: verde (derecha), roja, roja (ninguno), azul (derecha), verde, azul (izquierda).
        var sentidos = new[] { SentidoGancho.Derecha, SentidoGancho.Izquierda, SentidoGancho.Ninguno, SentidoGancho.Ninguno, SentidoGancho.Derecha, SentidoGancho.Izquierda };

        IReadOnlyList<IReadOnlyList<int>> grupos = GruposGancho.Separar(sentidos);

        Assert.Equal([0, 4], grupos[0]);
        Assert.Equal([2, 3], grupos[1]);
        Assert.Equal([1, 5], grupos[2]);
    }

    [Fact]
    public void ElGrupoATieneElGanchoALaIzquierdaYElBALaDerecha()
    {
        Assert.Equal(SentidoGancho.Izquierda, GruposGancho.DeGrupo(GrupoTraslapo.A));
        Assert.Equal(SentidoGancho.Derecha, GruposGancho.DeGrupo(GrupoTraslapo.B));
        Assert.Equal(SentidoGancho.Ninguno, GruposGancho.DeGrupo(null));
    }

    [Theory]
    [InlineData(new[] { 0.9 }, SentidoGancho.Derecha)]
    [InlineData(new[] { -1.0 }, SentidoGancho.Izquierda)]
    [InlineData(new[] { 0.1, -0.95 }, SentidoGancho.Izquierda)]
    [InlineData(new double[] { }, SentidoGancho.Ninguno)]
    [InlineData(new[] { 0.2 }, SentidoGancho.Ninguno)]
    public void ElSentidoDeLaLineaEsElDelPrimerGanchoDeLado(double[] direcciones, SentidoGancho esperado) =>
        Assert.Equal(esperado, GruposGancho.DeLinea(direcciones));
}
