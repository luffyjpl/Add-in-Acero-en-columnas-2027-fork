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

    // Decisión 19 del plan: cada lado se clasifica por hacia dónde mira.
    private static readonly Poligono LoteEnL = new([new(0, 0), new(12, 0), new(12, 8), new(6, 8), new(6, 16), new(0, 16)]);

    [Fact]
    public void En_un_lote_en_L_el_fondo_real_y_el_escalon_son_posteriores_y_el_costado_del_escalon_es_lateral_derecho() =>
        Assert.Equal([TipoLado.Frente, TipoLado.LateralDerecho, TipoLado.Posterior, TipoLado.LateralDerecho, TipoLado.Posterior, TipoLado.LateralIzquierdo],
            LadosLote.Clasificar(LoteEnL, 0));

    [Fact]
    public void En_el_lote_en_L_el_fondo_real_toma_el_retiro_posterior()
    {
        TipoLado[] tipos = LadosLote.Clasificar(LoteEnL, 0);
        Desfase d = DesfaseRetiros.Calcular(LoteEnL, tipos, new Retiros(1.5, 2, 1, 1));
        // Ala derecha 10 × 4,5 más ala izquierda 4 × 8.
        Assert.Equal(77, d.Util!.Area, 6);
    }

    [Fact]
    public void Un_lote_con_entrante_en_la_fachada_tiene_dos_lados_de_frente()
    {
        // La fachada se mete 2 m entre x = 4 y x = 8: el fondo del entrante también mira a la calle.
        var entrante = new Poligono([new(0, 0), new(4, 0), new(4, 2), new(8, 2), new(8, 0), new(12, 0), new(12, 14), new(0, 14)]);
        TipoLado[] tipos = LadosLote.Clasificar(entrante, 0);
        Assert.Equal(TipoLado.Frente, tipos[2]);
        Assert.Equal(TipoLado.Frente, tipos[4]);
        Assert.Equal(TipoLado.Posterior, tipos[6]);
    }

    [Fact]
    public void Un_triangulo_con_el_frente_en_la_base_tiene_dos_laterales_y_ningun_posterior() =>
        Assert.Equal([TipoLado.Frente, TipoLado.LateralDerecho, TipoLado.LateralIzquierdo],
            LadosLote.Clasificar(new Poligono([new(0, 0), new(20, 0), new(10, 20)]), 0));

    [Fact]
    public void Un_lado_que_mira_mas_hacia_atras_que_al_costado_es_posterior()
    {
        // Lote irregular de 5 lados: los dos lados de arriba miran más hacia el fondo que hacia el costado.
        var irregular = new Poligono([new(0, 0), new(9, -1), new(11, 7), new(6, 13), new(-1, 10)]);
        Assert.Equal([TipoLado.Frente, TipoLado.LateralDerecho, TipoLado.Posterior, TipoLado.Posterior, TipoLado.LateralIzquierdo],
            LadosLote.Clasificar(irregular, 0));
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
