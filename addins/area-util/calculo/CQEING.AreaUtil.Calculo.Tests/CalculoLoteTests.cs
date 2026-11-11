namespace CQEING.AreaUtil.Calculo.Tests;

public class CalculoLoteTests
{
    private static EntradaLote Lote7x14(Ficha? ficha = null, Retiros? retiros = null) =>
        new(Poligono.Rectangulo(7, 14).Vertices, 0, null, retiros ?? new Retiros(1.5, 2, 1, 1), ficha ?? new Ficha());

    [Fact]
    public void El_resumen_del_7_por_14()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14());
        Assert.True(r.SePuedeGenerar);
        Assert.Equal(98, r.Resumen!.AreaLote, 6);
        Assert.Equal(52.5, r.Resumen.AreaUtil, 6);
        Assert.Equal(45.5, r.Resumen.AreaRetiros, 6);
        Assert.Equal(7, r.Resumen.Frente, 6);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void Un_frente_menor_al_minimo_avisa_sin_bloquear()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(6.5, 14).Vertices, 0, null, new Retiros(1.5, 2, 1, 1), new Ficha(FrenteMinimo: 7)));
        Assert.True(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("frente") && !a.Bloquea);
    }

    [Fact]
    public void Un_lote_menor_al_minimo_avisa() =>
        Assert.Contains(CalculoLote.Calcular(Lote7x14(new Ficha(LoteMinimo: 120))).Avisos, a => a.Texto.Contains("lote") && !a.Bloquea);

    [Fact]
    public void Si_el_area_util_supera_el_imo_manda_el_indice()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14(new Ficha(Imo: 0.5)));
        Assert.Equal(49, r.Resumen!.OcupacionMaxima!.Value, 6);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("IMO"));
    }

    [Fact]
    public void Con_los_pisos_se_compara_con_el_imc()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14(new Ficha(Imc: 1, Pisos: 2)));
        Assert.Equal(105, r.Resumen!.ConstruccionPosible!.Value, 6);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("IMC"));
    }

    [Fact]
    public void Sin_ficha_no_hay_avisos_de_ficha() => Assert.Empty(CalculoLote.Calcular(Lote7x14()).Avisos);

    [Fact]
    public void Dos_puntos_no_son_un_lote()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote([new(0, 0), new(7, 0)], 0, null, new Retiros(1, 1, 1, 1), new Ficha()));
        Assert.False(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Bloquea);
    }

    [Fact]
    public void Un_lote_con_lados_cruzados_bloquea()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote([new(0, 0), new(7, 14), new(7, 0), new(0, 14)], 0, null, new Retiros(1, 1, 1, 1), new Ficha()));
        Assert.False(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Bloquea && a.Texto.Contains("cruzan"));
    }

    [Fact]
    public void Los_retiros_que_se_comen_un_lado_bloquean() =>
        Assert.False(CalculoLote.Calcular(Lote7x14(retiros: new Retiros(1.5, 2, 4, 4))).SePuedeGenerar);

    [Fact]
    public void Los_tipos_cambiados_a_mano_mandan_sobre_los_calculados()
    {
        TipoLado[] amano = [TipoLado.Frente, TipoLado.Frente, TipoLado.Posterior, TipoLado.LateralIzquierdo];
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, amano, new Retiros(1.5, 2, 1, 1), new Ficha()));
        Assert.Equal(amano, r.Tipos);
        Assert.Equal((7 - 1 - 1.5) * (14 - 1.5 - 2), r.Resumen!.AreaUtil, 6);
    }

    [Fact]
    public void Tipos_de_otro_largo_se_ignoran() =>
        Assert.Equal(TipoLado.Frente, CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, [TipoLado.Posterior], new Retiros(1, 1, 1, 1), new Ficha())).Tipos[0]);
}
