namespace CQEING.AreaUtil.Calculo.Tests;

public class LadosLoteTests
{
    [Fact]
    public void En_el_rectangulo_el_frente_abajo_deja_el_posterior_arriba_y_los_laterales_a_los_lados()
    {
        TipoLado[] tipos = LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 0);
        Assert.Equal([TipoLado.Frente, TipoLado.LateralDerecho, TipoLado.Posterior, TipoLado.LateralIzquierdo], tipos);
    }

    [Fact]
    public void En_sentido_horario_derecha_e_izquierda_siguen_siendo_las_de_quien_mira_desde_la_calle()
    {
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        // El frente es el lado de abajo (7,0)-(0,0), que en este orden es el lado 3.
        TipoLado[] tipos = LadosLote.Clasificar(horario, 3);
        Assert.Equal([TipoLado.LateralIzquierdo, TipoLado.Posterior, TipoLado.LateralDerecho, TipoLado.Frente], tipos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cualquier_lado_puede_ser_el_frente_y_el_opuesto_queda_posterior(int frente)
    {
        TipoLado[] tipos = LadosLote.Clasificar(Poligono.Rectangulo(7, 14), frente);
        Assert.Equal(TipoLado.Frente, tipos[frente]);
        Assert.Equal(TipoLado.Posterior, tipos[(frente + 2) % 4]);
    }

    [Fact]
    public void En_un_lote_en_L_hay_un_solo_frente_y_un_solo_posterior()
    {
        var l = new Poligono([new(0, 0), new(10, 0), new(10, 6), new(5, 6), new(5, 12), new(0, 12)]);
        TipoLado[] tipos = LadosLote.Clasificar(l, 0);
        Assert.Single(tipos, t => t == TipoLado.Frente);
        Assert.Single(tipos, t => t == TipoLado.Posterior);
    }

    [Fact]
    public void Un_indice_fuera_de_rango_se_toma_dando_la_vuelta() =>
        Assert.Equal(TipoLado.Frente, LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 5)[1]);

    [Fact]
    public void Cada_tipo_toma_su_retiro()
    {
        var r = new Retiros(1.5, 2, 1, 0.5);
        Assert.Equal(1.5, r.Para(TipoLado.Frente));
        Assert.Equal(2, r.Para(TipoLado.Posterior));
        Assert.Equal(1, r.Para(TipoLado.LateralIzquierdo));
        Assert.Equal(0.5, r.Para(TipoLado.LateralDerecho));
    }
}
