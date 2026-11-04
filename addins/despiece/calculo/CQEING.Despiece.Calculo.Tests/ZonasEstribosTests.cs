namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Estribos sueltos juntados en zonas (#227, hallazgo 29 del plan de columnas). Alturas en metros.</summary>
public class ZonasEstribosTests
{
    private static List<double> Seguidos(double desde, int cantidad, double separacion) =>
        Enumerable.Range(0, cantidad).Select(i => Math.Round(desde + i * separacion, 4)).ToList();

    [Fact]
    public void LosSeguidosConLaMismaSeparacionSonUnaZona()
    {
        ZonaEstribos z = Assert.Single(ZonasEstribos.Agrupar(Seguidos(0.05, 15, 0.14)));

        Assert.Equal(15, z.Cantidad);
        Assert.Equal(0.14, z.Separacion, 3);
        Assert.Equal(0.05 + 14 * 0.14 / 2, z.Centro, 3);
    }

    [Fact]
    public void UnCambioDeSeparacionEmpiezaOtraZona()
    {
        List<double> alturas = [.. Seguidos(0.05, 8, 0.07), .. Seguidos(0.05 + 7 * 0.07 + 0.14, 10, 0.14)];

        IReadOnlyList<ZonaEstribos> zonas = ZonasEstribos.Agrupar(alturas);

        Assert.Equal([(8, 0.07), (10, 0.14)], zonas.Select(z => (z.Cantidad, Math.Round(z.Separacion, 2))));
    }

    [Fact]
    public void EstriboYGrapaALaMismaAlturaCuentanUnaVez()
    {
        List<double> alturas = [.. Seguidos(0.10, 5, 0.20), .. Seguidos(0.102, 5, 0.20)];

        ZonaEstribos z = Assert.Single(ZonasEstribos.Agrupar(alturas));

        Assert.Equal(5, z.Cantidad);
    }

    [Fact]
    public void UnaSeparacionMuyGrandeCortaLaZona()
    {
        IReadOnlyList<ZonaEstribos> zonas = ZonasEstribos.Agrupar([.. Seguidos(0.10, 3, 0.10), 2.50]);

        Assert.Equal([3, 1], zonas.Select(z => z.Cantidad));
        Assert.Equal(0, zonas[1].Separacion);
    }

    [Fact]
    public void ElOrdenDeEntradaNoImporta()
    {
        List<double> alturas = Seguidos(0.05, 6, 0.10);
        alturas.Reverse();

        Assert.Equal(6, Assert.Single(ZonasEstribos.Agrupar(alturas)).Cantidad);
    }

    [Fact]
    public void UnaDiferenciaDeMilimetrosEnLaSeparacionNoCortaLaZona()
    {
        ZonaEstribos z = Assert.Single(ZonasEstribos.Agrupar([0.10, 0.24, 0.381, 0.519, 0.66]));

        Assert.Equal(5, z.Cantidad);
    }
}
