namespace CQEING.Despiece.Calculo.Tests;

/// <summary>La planta del tablero dibujada por la página (#285, #286): plan docs/planes/2026-11-05-reunion-despiece-287.md, paso 1.</summary>
public class ArmadorPlantaTests
{
    private static PuntoModelo P(double x, double y) => new(x, y);

    [Fact]
    public void LaEnvolventeDeUnRectanguloSonSusCuatroEsquinas()
    {
        // Las aristas de un sólido repiten esquinas y traen puntos a mitad de lado.
        var puntos = new[] { P(0, 0), P(4, 0), P(4, 0.3), P(0, 0.3), P(2, 0), P(0, 0), P(2, 0.15) };

        IReadOnlyList<PuntoModelo> casco = ArmadorPlanta.Envolvente(puntos);

        Assert.Equal(4, casco.Count);
        Assert.Equal(new[] { P(0, 0), P(4, 0), P(4, 0.3), P(0, 0.3) }.OrderBy(p => p.X).ThenBy(p => p.Y), casco.OrderBy(p => p.X).ThenBy(p => p.Y));
    }

    [Fact]
    public void ElRecuadroLlevaMargenYLaYVaHaciaAbajo()
    {
        var columna = new ElementoEnPlanta("1", true, [P(0, 0), P(0.4, 0), P(0.4, 0.4), P(0, 0.4)]);

        PlantaDibujada d = ArmadorPlanta.Armar([], [columna]);

        double m = ArmadorPlanta.Margen * ArmadorPlanta.PixelesPorMetro;
        Assert.Equal(2 * m + 0.4 * ArmadorPlanta.PixelesPorMetro, d.Ancho, 6);
        Assert.Equal(d.Ancho, d.Alto, 6);
        // La esquina (0, 0.4), arriba a la izquierda en el modelo, queda arriba a la izquierda en el dibujo.
        Assert.Contains(d.Elementos[0].Contorno, p => Math.Abs(p.X - m) < 1e-6 && Math.Abs(p.Y - m) < 1e-6);
    }

    [Fact]
    public void LosEjesSeRecortanAlDibujoYLosDeAfueraNoVan()
    {
        var viga = new ElementoEnPlanta("7", false, [P(0, 0), P(6, 0), P(6, 0.3), P(0, 0.3)]);
        var largo = new EjeLeido("A", P(-50, 0.15), P(50, 0.15));
        var lejos = new EjeLeido("Z", P(-50, 40), P(50, 40));

        PlantaDibujada d = ArmadorPlanta.Armar([largo, lejos], [viga]);

        EjeDibujado a = Assert.Single(d.Ejes);
        Assert.Equal("A", a.Nombre);
        Assert.Equal(0, Math.Min(a.X1, a.X2), 6);
        Assert.Equal(d.Ancho, Math.Max(a.X1, a.X2), 6);
    }

    [Fact]
    public void SinElementosElRecuadroEsElDeLosEjes()
    {
        PlantaDibujada d = ArmadorPlanta.Armar([new EjeLeido("1", P(0, 0), P(0, 5))], []);

        Assert.True(d.Alto > 0);
        Assert.Single(d.Ejes);
        Assert.Empty(d.Elementos);
    }

    [Fact]
    public void LosElementosSinPuntosNoVan()
    {
        PlantaDibujada d = ArmadorPlanta.Armar([], [new ElementoEnPlanta("1", false, []), new ElementoEnPlanta("2", false, [P(0, 0), P(1, 0), P(1, 1)])]);

        Assert.Equal("2", Assert.Single(d.Elementos).Id);
    }
}
