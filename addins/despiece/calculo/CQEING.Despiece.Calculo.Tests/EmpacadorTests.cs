namespace CQEING.Despiece.Calculo.Tests;

/// <summary>
/// El acomodador "lo más cerca sin tocar" (paso 6 del plan docs/planes/2026-11-10-despiece-santiago-14-15.md). "A lo
/// largo" es a lo largo de la viga (o de la columna); "afuera" es la distancia desde la viga hacia donde se sacan las barras.
/// </summary>
public class EmpacadorTests
{
    // Un grupo de un solo rectángulo que ocupa de su línea hacia afuera "alto".
    private static GrupoEmpacado Barra(double desde, double hasta, double alto = 0.1) => new([new Rectangulo(desde, hasta, 0, alto)]);

    [Fact]
    public void ElPrimeroVaPegadoALaBase() =>
        Assert.Equal([0.0], Empacador.Colocar([Barra(0, 5)], 0, 0, 0));

    [Fact]
    public void LoQueSobresaleHaciaLaVigaSeQuedaFueraDeElla()
    {
        // Un gancho que baja 0.2 desde la línea: la línea tiene que quedar a 0.2 de la base.
        var conGancho = new GrupoEmpacado([new Rectangulo(0, 5, 0, 0.1), new Rectangulo(0, 0.1, -0.2, 0)]);
        Assert.Equal([0.2], Empacador.Colocar([conGancho], 0, 0, 0));
    }

    [Fact]
    public void DosQueNoSeCruzanALoLargoQuedanEnLaMismaAltura() =>
        Assert.Equal([0.0, 0.0], Empacador.Colocar([Barra(0, 2), Barra(3, 5)], 0, 0.5, 0));

    [Fact]
    public void ElHuecoALoLargoSeRespeta() =>
        Assert.Equal([0.0, 0.15], Empacador.Colocar([Barra(0, 2), Barra(2.3, 5)], 0, 0.5, 0.05), new Cerca());

    [Fact]
    public void UnoEncimaDeOtroSeApoyaConElHueco() =>
        Assert.Equal([0.0, 0.15], Empacador.Colocar([Barra(0, 5), Barra(1, 4)], 0, 0, 0.05), new Cerca());

    [Fact]
    public void SoloEmpujaLoQueEstaALaMismaAlturaALoLargo()
    {
        // La fila de abajo tiene un texto alto (0.3) hacia su derecha y es baja (0.1) en el resto. La de encima es más
        // corta y tiene un gancho que baja 0.15 en su extremo izquierdo: no pasa sobre el texto, así que su gancho se
        // apoya en el 0.1 de la de abajo (con el mayor de todo, como antes, iría a 0.45).
        var abajo = new GrupoEmpacado([new Rectangulo(0, 10, 0, 0.1), new Rectangulo(8, 10, 0, 0.3)]);
        var arriba = new GrupoEmpacado([new Rectangulo(0, 6, 0, 0.05), new Rectangulo(0, 0.2, -0.15, 0)]);
        Assert.Equal([0.0, 0.25], Empacador.Colocar([abajo, arriba], 0, 0, 0), new Cerca());
    }

    [Fact]
    public void UnGanchoSobreElTextoDeLaOtraFilaEmpujaHastaPasarlo()
    {
        // El caso de la captura 15: el gancho de una fila cae justo sobre la etiqueta de la de abajo.
        var abajo = new GrupoEmpacado([new Rectangulo(0, 10, 0, 0.1), new Rectangulo(4, 6, 0, 0.3)]);
        var arriba = new GrupoEmpacado([new Rectangulo(5, 15, 0, 0.05), new Rectangulo(5, 5.2, -0.15, 0)]);
        Assert.Equal([0.0, 0.45], Empacador.Colocar([abajo, arriba], 0, 0, 0), new Cerca());
    }

    [Fact]
    public void MiraTodosLosYaPuestosYNoSoloElAnterior()
    {
        // La tercera es larga y cruza a la primera (alta) y a la segunda (corta y baja).
        var primera = Barra(0, 3, 0.5);
        var segunda = Barra(5, 8, 0.1);
        var tercera = Barra(0, 8, 0.1);
        Assert.Equal([0.0, 0.0, 0.5], Empacador.Colocar([primera, segunda, tercera], 0, 0, 0), new Cerca());
    }

    [Fact]
    public void EnOrdenCadaUnoVaMasAfueraQueElAnterior()
    {
        // Sin orden, la segunda (que no cruza a la primera) iría pegada a la base; en orden va después de ella.
        Assert.Equal([0.0, 0.0], Empacador.Colocar([Barra(0, 2), Barra(3, 5)], 0, 0, 0.05));
        Assert.Equal([0.0, 0.05], Empacador.Colocar([Barra(0, 2), Barra(3, 5)], 0, 0, 0.05, enOrden: true), new Cerca());
    }

    [Fact]
    public void SinOrdenUnoCortoCabeEnElHuecoQueDejaronOtros()
    {
        // Dos barras altas en carriles distintos dejan libre el primer carril más arriba: la corta entra ahí.
        var a = Barra(0, 4, 0.2);
        var b = Barra(2, 9, 0.2);
        var corta = Barra(6, 8, 0.1);
        Assert.Equal([0.0, 0.2, 0.0], Empacador.Colocar([a, b, corta], 0, 0, 0), new Cerca());
    }

    [Fact]
    public void UnObstaculoEmpujaSoloALoQueEstaASuAltura()
    {
        // Columna: una viga asoma a la derecha entre 2 y 3 de alto, hasta 0.3 de la cara. Una barra entre 0 y 1 sigue
        // pegada; una entre 1.5 y 4 va después de la viga.
        var viga = new Rectangulo(2, 3, 0, 0.3);
        Assert.Equal([0.0, 0.32], Empacador.Colocar([Barra(0, 1), Barra(1.5, 4)], 0, 0, 0.02, obstaculos: [viga]), new Cerca());
    }

    [Fact]
    public void ElMinimoDeCadaGrupoSeRespeta() =>
        Assert.Equal([0.0, 0.7], Empacador.Colocar([Barra(0, 2), Barra(3, 5) with { Minimo = 0.7 }], 0, 0, 0), new Cerca());

    [Fact]
    public void LaBaseCorreTodo() =>
        Assert.Equal([1.0, 1.15], Empacador.Colocar([Barra(0, 5), Barra(1, 4)], 1, 0, 0.05), new Cerca());

    private sealed class Cerca : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        public int GetHashCode(double obj) => 0;
    }
}
