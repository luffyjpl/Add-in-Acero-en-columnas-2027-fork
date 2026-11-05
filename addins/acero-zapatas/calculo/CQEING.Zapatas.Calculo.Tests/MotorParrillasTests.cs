using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo.Tests;

public class MotorParrillasTests
{
    private static readonly Reglas R = Reglas.Generales;

    private static List<Punto> Rect(double x, double y) => [new(0, 0), new(x, 0), new(x, y), new(0, y)];

    private static ConfigParrillas Config(int numero = 5, double sep = 150, double rec = 75, bool superior = true, Terminacion t = Terminacion.Pata90) =>
        new(new ConfigDireccion(numero, sep, t), new ConfigDireccion(numero, sep, t), Recubrimientos.Global(rec), superior);

    private static ZapataEntrada Maqueta() => new(Rect(1800, 1800), 500, [new(900, 900)]);

    [Fact]
    public void La_zapata_de_la_maqueta_da_12_barras_abajo_y_11_arriba_por_direccion()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(), R);

        Assert.Empty(r.Errores);
        Assert.Equal(46, r.Resumen.TotalBarras);
        foreach (ConjuntoBarras b in r.Conjuntos)
            Assert.Equal(b.Parrilla == Parrilla.Inferior ? 12 : 11, b.Cantidad);
    }

    [Fact]
    public void La_separacion_real_nunca_pasa_de_la_pedida()
    {
        ResultadoZapata r = MotorParrillas.Calcular(new ZapataEntrada(Rect(2345, 1777), 500, [new(1000, 800)]), Config(sep: 137), R);

        Assert.All(r.Conjuntos, b => Assert.True(b.Separacion <= 137.0001, $"{b.Separacion}"));
    }

    [Fact]
    public void La_pata_llega_al_recubrimiento_opuesto_en_las_dos_capas()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(), R);

        ConjuntoBarras primera = r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Capa == 1);
        ConjuntoBarras segunda = r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Capa == 2);
        Assert.Equal(350, primera.Pata, 3);
        Assert.Equal(350 - 15.9, segunda.Pata, 3);
    }

    [Fact]
    public void La_longitud_de_corte_sube_a_5_cm_y_el_peso_sale_de_ahi()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(superior: false), R);

        // 1650 de tramo + 2 × 350 de pata = 2350; la segunda capa da 2318.2 y sube a 2350.
        Assert.All(r.Conjuntos, b => Assert.Equal(2350, b.LongitudCorte));
        Assert.Equal(24 * 2350, r.Resumen.LongitudTotal);
        Assert.Equal(87.5, r.Resumen.Kilos, 1);
    }

    [Fact]
    public void Las_barras_de_una_parrilla_quedan_en_capas_que_no_se_cruzan()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(), R);

        ConjuntoBarras Capa(Parrilla p, int c) => r.Conjuntos.First(b => b.Parrilla == p && b.Capa == c);
        // Abajo: 75 + d/2 y encima, una barra más. Arriba, al revés desde la cara superior.
        Assert.Equal(82.95, Capa(Parrilla.Inferior, 1).Z, 3);
        Assert.Equal(98.85, Capa(Parrilla.Inferior, 2).Z, 3);
        Assert.Equal(417.05, Capa(Parrilla.Superior, 1).Z, 3);
        Assert.Equal(401.15, Capa(Parrilla.Superior, 2).Z, 3);
    }

    [Fact]
    public void Las_barras_corridas_empiezan_despues_de_la_franja_de_patas_de_la_otra_direccion()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(), R);

        // Recubrimiento 75, franja de 15.9 y medio diámetro.
        Assert.All(r.Conjuntos.Where(b => b.Parrilla == Parrilla.Inferior), b => Assert.Equal(98.85, b.Posiciones[0], 3));
    }

    [Fact]
    public void La_parrilla_superior_corre_media_separacion()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(), R);

        ConjuntoBarras abajo = r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Direccion == Direccion.X);
        ConjuntoBarras arriba = r.Conjuntos.First(b => b.Parrilla == Parrilla.Superior && b.Direccion == Direccion.X);
        Assert.Equal(abajo.Posiciones[0] + abajo.Separacion / 2, arriba.Posiciones[0], 6);
        Assert.Equal(abajo.Separacion, arriba.Separacion, 6);
    }

    [Theory]
    [InlineData(2000, 1500, Direccion.X)]
    [InlineData(1500, 2000, Direccion.Y)]
    [InlineData(1800, 1800, Direccion.X)]
    public void Abajo_va_primero_la_barra_paralela_al_lado_largo(double x, double y, Direccion principal)
    {
        ResultadoZapata r = MotorParrillas.Calcular(new ZapataEntrada(Rect(x, y), 500, [new(x / 2, y / 2)]), Config(), R);

        Assert.Equal(principal, r.Principal);
        Assert.Equal(principal, r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Capa == 1).Direccion);
        // Arriba también va hacia afuera: la misma dirección, pegada a su recubrimiento.
        Assert.Equal(principal, r.Conjuntos.First(b => b.Parrilla == Parrilla.Superior && b.Capa == 1).Direccion);
    }

    [Fact]
    public void En_una_combinada_la_principal_es_la_que_une_las_columnas()
    {
        var z = new ZapataEntrada(Rect(3000, 3200), 600, [new(500, 1600), new(2500, 1600)]);

        Assert.Equal(Direccion.X, MotorParrillas.Principal(z));
    }

    [Fact]
    public void Una_combinada_en_L_corta_cada_barra_contra_el_contorno()
    {
        List<Punto> ele = [new(0, 0), new(3000, 0), new(3000, 1000), new(1000, 1000), new(1000, 3000), new(0, 3000)];
        ResultadoZapata r = MotorParrillas.Calcular(new ZapataEntrada(ele, 500, [new(500, 500), new(2500, 500), new(500, 2500)]), Config(superior: false), R);

        Assert.Empty(r.Errores);
        var enX = r.Conjuntos.Where(b => b.Direccion == Direccion.X).OrderBy(b => b.Desde).ThenBy(b => b.Hasta).ToList();
        // Las barras en X miden 2850 en la pata larga de la L y 850 en la corta.
        Assert.Contains(enX, b => Math.Abs(b.Hasta - b.Desde - 2850) < 1);
        Assert.Contains(enX, b => Math.Abs(b.Hasta - b.Desde - 850) < 1);
        Assert.All(enX, b => Assert.True(b.Desde >= 75 - 0.001 && b.Hasta <= 3000 - 75 + 0.001));
    }

    [Fact]
    public void Una_pata_bajo_12_diametros_se_crea_y_avisa()
    {
        ResultadoZapata r = MotorParrillas.Calcular(new ZapataEntrada(Rect(1800, 1800), 300, [new(900, 900)]), Config(), R);

        Assert.Empty(r.Errores);
        Assert.NotEmpty(r.Conjuntos);
        Assert.Contains(r.Avisos, a => a.Contains("12 diámetros"));
    }

    [Fact]
    public void Una_pata_de_12_diametros_o_mas_no_avisa()
    {
        Assert.DoesNotContain(MotorParrillas.Calcular(Maqueta(), Config(), R).Avisos, a => a.Contains("12 diámetros"));
    }

    [Fact]
    public void Sin_parrilla_superior_solo_hay_inferior()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(superior: false), R);

        Assert.All(r.Conjuntos, b => Assert.Equal(Parrilla.Inferior, b.Parrilla));
    }

    [Fact]
    public void Un_gancho_de_180_suma_el_largo_de_vigas_sin_pata()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(superior: false, t: Terminacion.Gancho180), R);

        int gancho = Reglas.LargoGancho(R.PorDiametro[5], TipoGancho.De180);
        Assert.All(r.Conjuntos, b =>
        {
            Assert.Equal(0, b.Pata);
            Assert.Equal(1650 + 2 * gancho, b.LargoNeto);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Una_separacion_invalida_es_un_error(double sep)
    {
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(sep: sep), R);

        Assert.NotEmpty(r.Errores);
        Assert.Empty(r.Conjuntos);
    }

    [Fact]
    public void Una_zapata_que_no_da_para_el_recubrimiento_es_un_error()
    {
        Assert.NotEmpty(MotorParrillas.Calcular(new ZapataEntrada(Rect(100, 100), 500, [new(50, 50)]), Config(), R).Errores);
        Assert.NotEmpty(MotorParrillas.Calcular(new ZapataEntrada(Rect(1800, 1800), 150, [new(900, 900)]), Config(), R).Errores);
    }
}
