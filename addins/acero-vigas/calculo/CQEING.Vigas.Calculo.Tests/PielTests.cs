using CQEING.Vigas.Calculo.Nucleo;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Vigas.Calculo.Tests;

/// <summary>Acero de piel (06-nov) en el pórtico de la maqueta; las puntas son las de <see cref="BastonesTests"/>.</summary>
public class PielTests
{
    private static EntradaVigas Entrada(GrupoBarras? piel, TipoViga tipo = TipoViga.Aerea, CambiosManuales? cambios = null,
        Cara? cara = null) =>
        new(Datos.PorticoMaqueta(), tipo, [new GrupoBarras(3, 5)], [new GrupoBarras(3, 5)], Datos.ReglasSinRedondeo,
            CaraElegida: cara, Cambios: cambios, Piel: piel);

    [Fact]
    public void SinPiel_ElResultadoNoCambia()
    {
        ResultadoVigas sin = MotorVigas.Calcular(Entrada(null));
        ResultadoVigas cero = MotorVigas.Calcular(Entrada(new GrupoBarras(0, 4)));

        Assert.Null(sin.Piel);
        Assert.Null(cero.Piel);
        Assert.Equal(sin.Kg, cero.Kg);
    }

    [Fact]
    public void ConPiel_CorreTodoElPorticoHastaElLimite()
    {
        ResultadoVigas r = MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4)));

        Assert.Equal(-150, r.Piel!.Barras.First().Desde);
        Assert.Equal(13950, r.Piel.Barras.Last().Hasta);
        Assert.All(r.Piel.Barras, b => Assert.Equal([new GrupoBarras(4, 4)], b.Grupos));
        Assert.StartsWith("P", r.Piel.Barras[0].Id);
    }

    [Fact]
    public void ConPiel_PorDefectoSinGancho()
    {
        SolucionCapa piel = MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4))).Piel!;

        Assert.Equal(TipoGancho.Sin, piel.Barras.First().TipoInicio);
        Assert.Equal(TipoGancho.Sin, piel.Barras.Last().TipoFin);
    }

    [Fact]
    public void ConPiel_ElGanchoElegidoSeAplica()
    {
        var cambios = new CambiosManuales
        {
            Ganchos = new Dictionary<string, TipoGancho> { ["P.inicio"] = TipoGancho.De90, ["P.fin"] = TipoGancho.Sin },
        };
        ResultadoVigas r = MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4), cambios: cambios));

        Assert.Equal(TipoGancho.De90, r.Piel!.Barras.First().TipoInicio);
        Assert.True(r.Piel.Barras.First().GanchoInicio > 0);
        Assert.DoesNotContain(r.Avisos, a => a.Elemento == "P.fin");
    }

    [Fact]
    public void ConPiel_TraslapaConLaReferenciaDeArriba()
    {
        Assert.Equal(Referencia.Centro, MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4))).Piel!.Referencia);

        ResultadoVigas c = MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4), TipoViga.Cimentacion, cara: Cara.Final));
        Assert.Equal(c.Superior!.Referencia, c.Piel!.Referencia);
    }

    [Fact]
    public void ConPiel_LosKgCuentanLosDosLados()
    {
        ResultadoVigas sin = MotorVigas.Calcular(Entrada(null));
        ResultadoVigas con = MotorVigas.Calcular(Entrada(new GrupoBarras(2, 4)));
        double esperado = con.Piel!.Barras.Sum(b => b.Largo / 1000.0 * 4 * Datos.ReglasSinRedondeo.PorDiametro[4].KgPorMetro);

        Assert.Equal(esperado, con.Kg - sin.Kg, 6);
    }
}
