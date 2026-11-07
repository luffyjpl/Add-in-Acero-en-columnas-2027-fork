using CQEING.Vigas.Calculo.Tablero;

namespace CQEING.Vigas.Calculo.Tests;

/// <summary>
/// Punto 14 de Santiago: el selector de nivel de la planta dice qué pisos ya están construidos. Un piso está construido
/// cuando todos sus pórticos están terminados (Juan, 07-nov).
/// </summary>
public class AvanceNivelesTests
{
    private static ElementoPlanta Elemento(string portico, string tipo = "viga") => new(portico, tipo, 0, 0, 1, 0, 0.3);

    private static NivelPlanta Nivel(string nombre, params ElementoPlanta[] elementos) => new(nombre, 0, [], [], elementos);

    [Fact]
    public void CuentaLosPorticosDelNivelYLosTerminados()
    {
        var planta = new PlantaVista([
            Nivel("N+3.00", Elemento("Pórtico 1"), Elemento("Pórtico 2")),
            Nivel("N+6.00", Elemento("Pórtico 3"), Elemento("Pórtico 4"), Elemento("Pórtico 5")),
            Nivel("N+9.00", Elemento("Pórtico 6")),
        ]);

        PlantaVista con = AvanceNiveles.Con(planta, new HashSet<string> { "Pórtico 1", "Pórtico 2", "Pórtico 4" });

        Assert.Equal([(2, 2), (3, 1), (1, 0)], con.Niveles.Select(n => (n.Porticos, n.Terminados)));
        Assert.Equal([true, false, false], con.Niveles.Select(n => n.Construido));
    }

    [Fact]
    public void UnPorticoConVariasPiezasCuentaUnaVez()
    {
        // un pórtico de varias vigas (y un sistema de viguetas iguales) trae un elemento por pieza en la planta
        var planta = new PlantaVista([Nivel("N+3.00", Elemento("Pórtico 1"), Elemento("Pórtico 1"), Elemento("Vigueta 1", "vigueta"))]);

        NivelPlanta n = Assert.Single(AvanceNiveles.Con(planta, new HashSet<string> { "Pórtico 1" }).Niveles);

        Assert.Equal((2, 1, false), (n.Porticos, n.Terminados, n.Construido));
    }

    [Fact]
    public void UnNivelSinPorticosNoEstaConstruido()
    {
        NivelPlanta n = Assert.Single(AvanceNiveles.Con(new PlantaVista([Nivel("N+0.00")]), new HashSet<string>()).Niveles);

        Assert.Equal((0, 0, false), (n.Porticos, n.Terminados, n.Construido));
    }
}
