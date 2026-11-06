using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Porticos;
using CQEING.Vigas.Calculo.Seccion;
using CQEING.Vigas.Calculo.Tablero;

namespace CQEING.Vigas.Calculo.Tests;

/// <summary>
/// El cálculo de un pórtico que comparten la vista del tablero y la generación en Revit (#85): lo que se genera tiene
/// que ser lo mismo que se ve.
/// </summary>
public class CalculoPorticoTests
{
    [Fact]
    public void MismoResultadoQueLaVista()
    {
        var o = OpcionesTablero.PorDefecto with { Superior = [new GrupoBarras(3, 5), new GrupoBarras(2, 4)] };

        PorticoCalculado c = CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), o, Reglas.Generales)!;
        VistaPortico v = ArmadorVista.Armar(ArmadorVistaTests.Portico1(), o, Reglas.Generales);

        Assert.Equal(v.Kg, c.Vigas.Kg + c.Estribos.Kg, 6);
        Assert.Equal(v.SePuedeGenerar, c.SePuedeGenerar);
        Assert.True(c.SePuedeGenerar);
        Assert.Equal(v.Barras.Select(b => b.Id), c.Vigas.Barras.Select(b => b.Id));
        Assert.Equal(v.Estribos, c.Estribos.Cantidad);
    }

    [Fact]
    public void NoSePuedeGenerarSiLasBarrasNoCaben()
    {
        var o = OpcionesTablero.PorDefecto with { Superior = [new GrupoBarras(30, 8)] };

        PorticoCalculado c = CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), o, Reglas.Generales)!;

        Assert.False(c.SePuedeGenerar);
        Assert.Equal(ArmadorVista.Armar(ArmadorVistaTests.Portico1(), o, Reglas.Generales).SePuedeGenerar, c.SePuedeGenerar);
    }

    /// <summary>Una vigueta de 10 × 35 con 1 #4 arriba y abajo y recubrimiento 2.5 (#941).</summary>
    private static PorticoLeido Vigueta(Elemento elemento = Elemento.Vigueta)
    {
        PorticoLeido p = ArmadorVistaTests.Portico1();
        return p with { Elemento = elemento, Tramos = p.Tramos.Select(t => t with { Ancho = 100, Alto = 350 }).ToList() };
    }

    private static readonly OpcionesTablero UnaBarra = OpcionesTablero.PorDefecto with
    {
        Superior = [new GrupoBarras(1, 4)], Inferior = [new GrupoBarras(1, 4)], RecubrimientoCm = 2.5,
    };

    [Fact]
    public void Calcular_ConPiel_UbicaLaPielConElTramoMasBajo()
    {
        PorticoLeido p = ArmadorVistaTests.Portico1();
        p = p with { Tramos = p.Tramos.Select((t, i) => t with { Alto = i % 2 == 0 ? 500 : 400 }).ToList() };
        var o = OpcionesTablero.PorDefecto with { Piel = new GrupoBarras(2, 4) };

        PorticoCalculado c = CalculoPortico.Calcular(p, o, Reglas.Generales)!;

        EntradaSeccion s = CalculoPortico.Seccion(p, o, Reglas.Generales);
        UbicacionPiel esperada = RepartidorPiel.Ubicar(s, RepartidorFilas.Repartir(s), new GrupoBarras(2, 4), 400);
        UbicacionPiel con500 = RepartidorPiel.Ubicar(s, RepartidorFilas.Repartir(s), new GrupoBarras(2, 4), 500);
        Assert.Equal(2, c.Piel!.Alturas.Count);
        Assert.Equal(esperada.Alturas.Select(a => a.Izquierda.Y), c.Piel.Alturas.Select(a => a.Izquierda.Y));
        Assert.NotEqual(con500.Alturas[0].Izquierda.Y, c.Piel.Alturas[0].Izquierda.Y);
        Assert.NotNull(c.Vigas.Piel);
    }

    [Fact]
    public void Calcular_ConPielQueNoCabe_LaEntradaVaSinPiel()
    {
        var o = OpcionesTablero.PorDefecto with { Piel = new GrupoBarras(30, 4) };

        PorticoCalculado c = CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), o, Reglas.Generales)!;

        int caben = c.Piel!.Alturas.Count;
        Assert.InRange(caben, 1, 29);
        Assert.All(c.Vigas.Piel!.Barras, b => Assert.Equal([new GrupoBarras(2 * caben, 4)], b.Grupos));
    }

    [Fact]
    public void Calcular_Vigueta_NoPonePielYAvisa()
    {
        var o = UnaBarra with { Piel = new GrupoBarras(2, 4) };

        PorticoCalculado c = CalculoPortico.Calcular(Vigueta(), o, Reglas.Generales)!;

        Assert.Null(c.Vigas.Piel);
        Assert.Null(c.Piel);
        Assert.Contains(c.Avisos, a => a.Tipo == TipoAviso.PielNoPermitida);
        Assert.Equal(CalculoPortico.Calcular(Vigueta(), UnaBarra, Reglas.Generales)!.SePuedeGenerar, c.SePuedeGenerar);
    }

    [Fact]
    public void DiametrosSinReglas_IncluyeLaPiel()
    {
        var o = OpcionesTablero.PorDefecto with { Piel = new GrupoBarras(2, 2) };
        Reglas sinDos = Reglas.Generales with { PorDiametro = Reglas.Generales.PorDiametro.Where(k => k.Key != 2).ToDictionary(k => k.Key, k => k.Value) };

        Assert.Contains(2, CalculoPortico.DiametrosSinReglas(o, sinDos));
        Assert.Null(CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), o, sinDos));
    }

    [Fact]
    public void UnaViguetaConUnaBarraPorCara_SePuedeGenerar()
    {
        PorticoCalculado c = CalculoPortico.Calcular(Vigueta(), UnaBarra, Reglas.Generales)!;

        Assert.True(c.SePuedeGenerar);
        Assert.Equal(ErrorCara.Ninguno, c.Estribos.Seccion.Superior.Error);
    }

    [Fact]
    public void UnaVigueta_LlevaGrapaEnSYUnaVigaElCerrado()
    {
        // Decisión 8 del plan de #938: la Vigueta T1 del plano no tiene cerrado.
        PorticoCalculado vigueta = CalculoPortico.Calcular(Vigueta(), UnaBarra, Reglas.Generales)!;
        PorticoCalculado viga = CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), OpcionesTablero.PorDefecto, Reglas.Generales)!;

        Assert.All(vigueta.Estribos.Estribos.Where(e => e.Id == "E"), e => Assert.Equal((TipoEstribo.Abierto, FormaGrapa.S), (e.Tipo, e.Forma)));
        Assert.All(viga.Estribos.Estribos.Where(e => e.Id == "E"), e => Assert.Equal(TipoEstribo.Cerrado, e.Tipo));
    }

    [Fact]
    public void UnaVigaConUnaBarraPorCara_SigueSinPoderse()
    {
        Assert.False(CalculoPortico.Calcular(Vigueta(Elemento.Viga), UnaBarra, Reglas.Generales)!.SePuedeGenerar);
    }

    [Fact]
    public void LoQueCorrigeElUsuario_DecideSiPermiteUnaBarra()
    {
        // #940: el chip "Vigueta" manda sobre lo detectado.
        Assert.True(CalculoPortico.Calcular(Vigueta(Elemento.Viga), UnaBarra with { Elemento = Elemento.Vigueta }, Reglas.Generales)!.SePuedeGenerar);
        Assert.False(CalculoPortico.Calcular(Vigueta(), UnaBarra with { Elemento = Elemento.Viga }, Reglas.Generales)!.SePuedeGenerar);
    }

    [Fact]
    public void DiametroSinReglas()
    {
        var o = OpcionesTablero.PorDefecto with { Superior = [new GrupoBarras(3, 9)] };

        Assert.Null(CalculoPortico.Calcular(ArmadorVistaTests.Portico1(), o, Reglas.Generales));
    }
}
