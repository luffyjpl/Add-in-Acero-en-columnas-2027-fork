namespace CQEING.AreaUtil.Calculo.Tests;

public class PoligonoTests
{
    [Fact]
    public void El_rectangulo_de_7_por_14_mide_98_m2() =>
        Assert.Equal(98, Poligono.Rectangulo(7, 14).Area, 6);

    [Fact]
    public void Con_el_primer_punto_repetido_al_final_tiene_cuatro_lados()
    {
        var p = new Poligono([new(0, 0), new(7, 0), new(7, 14), new(0, 14), new(0, 0)]);
        Assert.Equal(4, p.Lados);
    }

    [Fact]
    public void Quita_puntos_repetidos_y_alineados()
    {
        var p = new Poligono([new(0, 0), new(3, 0), new(3, 0), new(7, 0), new(7, 14), new(0, 14)]);
        Assert.Equal(4, p.Lados);
    }

    [Fact]
    public void Con_coordenadas_topograficas_grandes_el_area_no_pierde_precision()
    {
        var p = new Poligono([new(1_200_000, 900_000), new(1_200_007, 900_000), new(1_200_007, 900_014), new(1_200_000, 900_014)]);
        Assert.Equal(98, p.Area, 6);
    }

    [Fact]
    public void Horario_y_antihorario_miden_lo_mismo()
    {
        var anti = Poligono.Rectangulo(7, 14);
        var horario = new Poligono(anti.Vertices.Reverse());
        Assert.True(anti.EsAntihorario);
        Assert.False(horario.EsAntihorario);
        Assert.Equal(anti.Area, horario.Area, 6);
    }

    [Fact]
    public void La_normal_hacia_adentro_del_frente_apunta_al_lote_en_los_dos_sentidos()
    {
        var anti = Poligono.Rectangulo(7, 14);
        Assert.Equal(new Punto(0, 1), anti.NormalAdentro(0));
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        Assert.Equal(new Punto(1, 0), horario.NormalAdentro(0));
    }

    [Fact]
    public void Un_mono_de_lados_cruzados_se_detecta()
    {
        var p = new Poligono([new(0, 0), new(7, 14), new(7, 0), new(0, 14)]);
        Assert.True(p.SeCruza());
        Assert.False(Poligono.Rectangulo(7, 14).SeCruza());
    }

    [Fact]
    public void Contiene_distingue_adentro_y_afuera()
    {
        var p = Poligono.Rectangulo(7, 14);
        Assert.True(p.Contiene(new(3, 3)));
        Assert.False(p.Contiene(new(8, 3)));
    }

    [Fact]
    public void Un_punto_en_el_borde_o_en_una_esquina_no_queda_afuera()
    {
        var p = Poligono.Rectangulo(7, 14);
        Assert.True(p.ContieneOBorde(new(7, 14)));
        Assert.True(p.ContieneOBorde(new(0, 5)));
        Assert.False(p.ContieneOBorde(new(7.01, 5)));
    }

    [Fact]
    public void La_interseccion_de_dos_rectas()
    {
        Assert.True(Rectas.Interseccion(new(0, 1.5), new(1, 0), new(1, 0), new(0, 1), out Punto p));
        Assert.Equal(new Punto(1, 1.5), p);
        Assert.False(Rectas.Interseccion(new(0, 0), new(1, 0), new(0, 1), new(1, 0), out _));
    }
}
