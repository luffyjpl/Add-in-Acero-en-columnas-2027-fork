namespace CQEING.Despiece.Calculo.Tests;

public class EstribosPrincipalesTests
{
    [Fact]
    public void LaGrapaALaAlturaDelEstriboEsMenor()
    {
        var formas = new[]
        {
            new FormaEstribo("estribo", 1.80, 0.10, 2.90),
            new FormaEstribo("grapa", 0.60, 0.12, 2.88),
        };

        Assert.Equal(["estribo"], EstribosPrincipales.Filtrar(formas));
    }

    [Fact]
    public void DosPisosConEstribosDistintosSonPrincipalesLosDos()
    {
        var formas = new[]
        {
            new FormaEstribo("piso 1", 1.80, 0.10, 2.90),
            new FormaEstribo("piso 2", 1.60, 3.10, 5.90),
        };

        Assert.Equal(["piso 1", "piso 2"], EstribosPrincipales.Filtrar(formas));
    }

    [Fact]
    public void LaCajaDeUnPisoNoTapaAlEstriboDelOtro()
    {
        var formas = new[]
        {
            new FormaEstribo("piso 1", 1.80, 0.10, 2.90),
            new FormaEstribo("caja piso 2", 1.20, 3.10, 5.90),
            new FormaEstribo("piso 2", 1.60, 3.10, 5.90),
        };

        Assert.Equal(["piso 1", "piso 2"], EstribosPrincipales.Filtrar(formas));
    }

    [Fact]
    public void IgualDeLargosSonPrincipalesLosDos()
    {
        var formas = new[]
        {
            new FormaEstribo("a", 1.80, 0.10, 2.90),
            new FormaEstribo("b", 1.80, 0.10, 2.90),
        };

        Assert.Equal(["a", "b"], EstribosPrincipales.Filtrar(formas));
    }
}
