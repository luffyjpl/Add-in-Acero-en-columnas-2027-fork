namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Etiquetas de estribos de una columna, a la altura de su zona y sin encimarse (#227, decisión 14).</summary>
public class EtiquetasEstribosTests
{
    [Fact]
    public void LasSeparadasQuedanEnElCentroDeSuZona()
    {
        Assert.Equal([0.5, 2.0, 4.0], EtiquetasEstribos.SinEncimar([0.5, 2.0, 4.0], 0.13));
    }

    [Fact]
    public void LaQueQuedaMuyCercaSubeLoJusto()
    {
        IReadOnlyList<double> z = EtiquetasEstribos.SinEncimar([1.0, 1.05, 1.10], 0.13);

        Assert.Equal(1.0, z[0], 6);
        Assert.Equal(1.13, z[1], 6);
        Assert.Equal(1.26, z[2], 6);
    }

    [Fact]
    public void DevuelveEnElOrdenEnQueLlegaron()
    {
        IReadOnlyList<double> z = EtiquetasEstribos.SinEncimar([3.0, 1.0, 1.05], 0.13);

        Assert.Equal(3.0, z[0], 6);
        Assert.Equal(1.0, z[1], 6);
        Assert.Equal(1.13, z[2], 6);
    }
}
