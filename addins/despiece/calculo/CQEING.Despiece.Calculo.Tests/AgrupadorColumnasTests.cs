namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Columnas iguales (#226): plan docs/planes/2026-10-31-despiece-columnas-226-228.md, paso 2.</summary>
public class AgrupadorColumnasTests
{
    private static BarraLeida Barra(int largo = 3532, int posicion = 1300, string tipo = "#4 CQEING", string forma = "CQ Recta 90-0") =>
        new(tipo, forma, 1, largo, posicion);

    private static ElementoLeido Columna(string id, string ejes, params BarraLeida[] barras) =>
        new(id, ejes, 400, 400, 7650, barras, EsColumna: true);

    [Fact]
    public void DosColumnasConElMismoAceroLlevanUnSoloDespiece()
    {
        var grupos = AgrupadorIguales.Agrupar([Columna("1", "1-6", Barra()), Columna("2", "2-6", Barra()), Columna("3", "1-1", Barra(largo: 7032))]);

        // Como la marca de Revit, separadas con coma: con guion no se sabría dónde termina cada una (decisión 15).
        Assert.Equal(["Col 1-6, 2-6 - (0.40x0.40)", "Col 1-1 - (0.40x0.40)"], grupos.Select(g => g.Nombre));
    }

    [Fact]
    public void UnaColumnaNoSeComparaAlReves()
    {
        // Abajo y arriba no son intercambiables: la barra con el gancho en la zapata no es la de la cubierta.
        var grupos = AgrupadorIguales.Agrupar([Columna("1", "A2", Barra(posicion: 1300)), Columna("2", "B2", Barra(posicion: 7650 - 1300))]);

        Assert.Equal(2, grupos.Count);
    }

    [Fact]
    public void UnaVigaYUnaColumnaConLoMismoNoSeJuntan()
    {
        var viga = new ElementoLeido("1", "A", 400, 400, 7650, [Barra()]);

        var grupos = AgrupadorIguales.Agrupar([viga, Columna("2", "A-2", Barra())]);

        Assert.Equal(["V A - (0.40x0.40)", "Col A-2 - (0.40x0.40)"], grupos.Select(g => g.Nombre));
    }

    [Fact]
    public void LasVigasNoCambianDeNombre()
    {
        var viga = new ElementoLeido("1", "A-B", 250, 350, 6000, [Barra()]);

        Assert.Equal("V A-B - (0.25x0.35)", Assert.Single(AgrupadorIguales.Agrupar([viga])).Nombre);
    }

    [Theory]
    [InlineData("1-6", "1-6")]
    [InlineData("C-2", "C-2")]
    [InlineData("C(-0.15)-2", "C(-0.15)-2")]
    [InlineData("C(-150)-2(75)", "C(-150)-2(75)")]
    [InlineData(" B-3 ", "B-3")]
    [InlineData("", "")]
    public void LaMarcaDeRevitTalCual(string marca, string nombre)
    {
        Assert.Equal(nombre, NombreColumna.DeMarca(marca));
    }
}
