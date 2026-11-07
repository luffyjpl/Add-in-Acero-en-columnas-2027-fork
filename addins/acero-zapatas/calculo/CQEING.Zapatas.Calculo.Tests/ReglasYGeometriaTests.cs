using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo.Tests;

public class ReglasYGeometriaTests
{
    private static readonly Reglas R = Reglas.Generales;

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Los_ganchos_miden_lo_mismo_que_en_vigas(int numero)
    {
        Assert.Equal(Reglas.LargoGancho(R.PorDiametro[numero], TipoGancho.De135), ReglasZapata.LargoGancho(R, numero, Terminacion.Gancho135));
        Assert.Equal(Reglas.LargoGancho(R.PorDiametro[numero], TipoGancho.De180), ReglasZapata.LargoGancho(R, numero, Terminacion.Gancho180));
    }

    [Theory]
    [InlineData(2318.2, 2350)]
    [InlineData(2350, 2350)]
    [InlineData(2350.5, 2400)]
    public void La_longitud_de_corte_sube_a_multiplos_de_5_cm(double neto, double corte) =>
        Assert.Equal(corte, ReglasZapata.LongitudCorte(neto));

    [Fact]
    public void El_aviso_de_tabla_no_salta_con_un_gancho_del_cuadro()
    {
        Assert.Null(ReglasZapata.AvisoTabla(R, 5, 90, 95));
        Assert.Null(ReglasZapata.AvisoTabla(R, 5, 180, null));
    }

    [Fact]
    public void El_aviso_de_tabla_salta_por_el_angulo_o_el_doblado()
    {
        Assert.NotNull(ReglasZapata.AvisoTabla(R, 5, 45, 95));
        Assert.NotNull(ReglasZapata.AvisoTabla(R, 5, 90, 60));
    }

    [Fact]
    public void Los_ejes_de_una_zapata_girada_salen_de_sus_lados()
    {
        List<Punto> girada = Geometria.Girar([new(0, 0), new(2000, 0), new(2000, 1000), new(0, 1000)], Math.PI / 6);

        Assert.Equal(Math.PI / 6, Geometria.AnguloEjes(girada), 6);
    }

    [Fact]
    public void Un_rombo_con_su_columna_recta_lleva_las_barras_en_los_ejes_del_proyecto()
    {
        List<Punto> rombo = [new(1000, 0), new(0, 1000), new(-1000, 0), new(0, -1000)];

        Assert.Equal(0, Geometria.AnguloEjes(rombo, [0]), 6);
    }

    [Fact]
    public void Una_zapata_girada_con_su_columna_girada_sigue_a_la_columna()
    {
        List<Punto> girada = Geometria.Girar([new(0, 0), new(2000, 0), new(2000, 1000), new(0, 1000)], Math.PI / 6);

        Assert.Equal(Math.PI / 6, Geometria.AnguloEjes(girada, [Math.PI / 6 + Math.PI / 2]), 6);
    }

    [Fact]
    public void Sin_columna_el_rombo_sale_de_sus_lados()
    {
        List<Punto> rombo = [new(1000, 0), new(0, 1000), new(-1000, 0), new(0, -1000)];

        Assert.Equal(Math.PI / 4, Math.Abs(Geometria.AnguloEjes(rombo, [])), 6);
    }

    [Fact]
    public void Un_rectangulo_sin_girar_tiene_angulo_cero()
    {
        Assert.Equal(0, Geometria.AnguloEjes([new(0, 0), new(2000, 0), new(2000, 1000), new(0, 1000)]), 6);
    }

    [Fact]
    public void El_contorno_hacia_adentro_conserva_una_L()
    {
        List<Punto> dentro = Geometria.HaciaAdentro([new(0, 0), new(3000, 0), new(3000, 1000), new(1000, 1000), new(1000, 3000), new(0, 3000)], 100);

        Assert.Equal(6, dentro.Count);
        Assert.Contains(dentro, p => Math.Abs(p.X - 900) < 1e-6 && Math.Abs(p.Y - 900) < 1e-6);
        // Área de la L menor: 2800 × 800 + 800 × 2000.
        Assert.Equal(3_840_000, Math.Abs(Geometria.Area(dentro)), 3);
    }

    [Fact]
    public void Un_contorno_que_no_da_para_el_recubrimiento_queda_vacio()
    {
        Assert.Empty(Geometria.HaciaAdentro([new(0, 0), new(100, 0), new(100, 100), new(0, 100)], 80));
    }

    private static ZapataParaAgrupar Z(string id, double x, double y, double h, double dx = 0, double dy = 0) =>
        new(id, [new(dx, dy), new(dx + x, dy), new(dx + x, dy + y), new(dx, dy + y)], h);

    [Fact]
    public void Las_zapatas_iguales_se_agrupan_aunque_esten_en_otro_sitio()
    {
        var grupos = AgrupadorZapatas.Agrupar([Z("A1", 1800, 1800, 500), Z("A2", 1800, 1800, 500, 5000, 3000), Z("B", 2000, 1800, 500), Z("A3", 1800.5, 1800, 500)]);

        Assert.Equal(2, grupos.Count);
        Assert.Equal(["A1", "A2", "A3"], grupos[0]);
        Assert.Equal(["B"], grupos[1]);
    }

    [Fact]
    public void Otra_altura_es_otra_zapata()
    {
        Assert.False(AgrupadorZapatas.SonIguales(Z("A", 1800, 1800, 500), Z("B", 1800, 1800, 600)));
    }

    [Fact]
    public void El_mismo_contorno_se_reconoce_desde_otro_vertice_y_en_otro_sentido()
    {
        var a = new ZapataParaAgrupar("A", [new(0, 0), new(2000, 0), new(2000, 1000), new(0, 1000)], 500);
        var b = new ZapataParaAgrupar("B", [new(2000, 1000), new(2000, 0), new(0, 0), new(0, 1000)], 500);

        Assert.True(AgrupadorZapatas.SonIguales(a, b));
    }
}
