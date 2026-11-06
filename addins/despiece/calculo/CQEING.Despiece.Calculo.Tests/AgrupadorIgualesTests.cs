namespace CQEING.Despiece.Calculo.Tests;

public class AgrupadorIgualesTests
{
    private static BarraLeida Barra(int largo = 6000, int posicion = 3000, string tipo = "#5", string forma = "00", int cantidad = 3) =>
        new(tipo, forma, cantidad, largo, posicion);

    private static ElementoLeido Viga(string id, string ejes, params BarraLeida[] barras) =>
        new(id, ejes, 500, 900, 10000, barras);

    [Fact]
    public void DosVigasConElMismoAceroSalenJuntas()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra()), Viga("2", "B", Barra())]);

        Assert.Equal(["1", "2"], Assert.Single(grupos).Elementos.Select(e => e.Id));
    }

    [Fact]
    public void UnLargoDistintoLasSepara()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra(largo: 6000)), Viga("2", "B", Barra(largo: 5500))]);

        Assert.Equal(2, grupos.Count);
    }

    [Fact]
    public void UnaSeccionDistintaLasSepara()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra()), Viga("2", "B", Barra()) with { AltoMm = 600 }]);

        Assert.Equal(2, grupos.Count);
    }

    [Fact]
    public void ElOrdenDeLasBarrasEnLaListaNoImporta()
    {
        BarraLeida a = Barra(largo: 6000), b = Barra(largo: 2500, posicion: 8000);

        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", a, b), Viga("2", "B", b, a)]);

        Assert.Single(grupos);
    }

    [Fact]
    public void LaMismaVigaDibujadaAlReves()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra(posicion: 3000)), Viga("2", "B", Barra(posicion: 7000))]);

        Assert.Single(grupos);
    }

    [Fact]
    public void UnaDiferenciaDeRedondeoNoLasSepara()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra(largo: 6000)), Viga("2", "B", Barra(largo: 6002))]);

        Assert.Single(grupos);
    }

    [Fact]
    public void SinBarrasNoFormaGrupoConOtraSinBarras()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A"), Viga("2", "B")]);

        Assert.Equal(2, grupos.Count);
        Assert.All(grupos, g => Assert.False(g.TieneAcero));
    }

    [Fact]
    public void ElNombreLlevaEjesYSeccion()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "C", Barra()), Viga("2", "A", Barra()), Viga("3", "B", Barra())]);

        Assert.Equal("V A-B-C - (0.50x0.90)", Assert.Single(grupos).Nombre);
    }

    [Fact]
    public void DosVigasDelMismoEjeNoRepitenElEje()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra()), Viga("2", "A", Barra())]);

        Assert.Equal("V A - (0.50x0.90)", Assert.Single(grupos).Nombre);
    }

    [Fact]
    public void SinEjeElNombreLlevaSoloLaSeccion()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "", Barra())]);

        Assert.Equal("V - (0.50x0.90)", Assert.Single(grupos).Nombre);
    }

    [Fact]
    public void ElMismoEjeEnDosPisosConAceroDistintoNoSeLlamaIgual()
    {
        var grupos = AgrupadorIguales.Agrupar([
            Viga("1", "A", Barra(tipo: "#4")) with { Nivel = "N+3.70" },
            Viga("2", "A", Barra(tipo: "#5")) with { Nivel = "N+6.70" },
        ]);

        Assert.Equal(["V N+3.70 A - (0.50x0.90)", "V N+6.70 A - (0.50x0.90)"], grupos.Select(g => g.Nombre));
    }

    [Fact]
    public void SinRepetirseElNombreNoLlevaElNivel()
    {
        var grupos = AgrupadorIguales.Agrupar([Viga("1", "A", Barra()) with { Nivel = "N+3.70" }]);

        Assert.Equal("V A - (0.50x0.90)", Assert.Single(grupos).Nombre);
    }

    [Fact]
    public void ElMismoEjeYPisoConAceroDistintoSeNumera()
    {
        var grupos = AgrupadorIguales.Agrupar([
            Viga("1", "A", Barra(tipo: "#4")) with { Nivel = "N+3.70" },
            Viga("2", "A", Barra(tipo: "#5")) with { Nivel = "N+3.70" },
        ]);

        Assert.Equal(["V N+3.70 A - (0.50x0.90)", "V N+3.70 A - (0.50x0.90) (2)"], grupos.Select(g => g.Nombre));
    }

    [Fact]
    public void ColumnasConLosGanchosDeOtroLadoNoSonIguales()
    {
        // La 1-6, de esquina junto al lindero, tiene ganchos hacia afuera; la 5-7 y la 5-8, todos hacia adentro (decisión 19 del plan de #287).
        static ElementoLeido Columna(string id, string ejes, string gancho) =>
            new(id, ejes, 300, 300, 7000, [new BarraLeida("#6", "CQ Recta 0-90", 1, 3500, 1000, 3210, gancho)], EsColumna: true);

        IReadOnlyList<GrupoIguales> grupos = AgrupadorIguales.Agrupar([Columna("1", "1-6", "F"), Columna("2", "5-7", "A"), Columna("3", "5-8", "A")]);

        Assert.Equal(["Col 1-6 - (0.30x0.30)", "Col 5-7, 5-8 - (0.30x0.30)"], grupos.Select(g => g.Nombre).Order());
    }
}
