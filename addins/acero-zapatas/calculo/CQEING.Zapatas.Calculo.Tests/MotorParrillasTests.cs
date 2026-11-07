using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo.Tests;

public class MotorParrillasTests
{
    private static readonly Reglas R = Reglas.Generales;

    private static List<Punto> Rect(double x, double y) => [new(0, 0), new(x, 0), new(x, y), new(0, y)];

    // Las pruebas de la v0.1.0 miden la pata hasta el recubrimiento; la del cuadro tiene las suyas abajo (decisión 29).
    private static ConfigParrillas Config(int numero = 5, double sep = 150, double rec = 75, bool superior = true, Terminacion t = Terminacion.Pata90,
        LargoPata largo = LargoPata.Recubrimiento) =>
        new(new ConfigDireccion(numero, sep, t), new ConfigDireccion(numero, sep, t), Recubrimientos.Global(rec), superior, LargoPata: largo);

    private static ZapataEntrada Maqueta() => new(Rect(1800, 1800), 500, [new(900, 900)]);

    [Fact]
    public void En_las_puntas_de_un_rombo_no_quedan_barras_donde_no_caben_los_doblados()
    {
        // La zapata 933661 del modelo de prueba: 1.55 m de lado girada 45°, 0.90 m de alto. Sin el mínimo, las puntas
        // daban barras de 47 mm con patas de 750 mm y Revit las creaba sin geometría.
        var rombo = new ZapataEntrada([new(1096, 0), new(0, 1096), new(-1096, 0), new(0, -1096)], 900, [new(0, 0)]);

        ResultadoZapata r = MotorParrillas.Calcular(rombo, Config(), R);

        double minimo = ReglasZapata.LargoMinimo(R.PorDiametro[5], Terminacion.Pata90);
        Assert.Empty(r.Errores);
        Assert.NotEmpty(r.Conjuntos);
        Assert.All(r.Conjuntos, c => Assert.True(c.Hasta - c.Desde >= minimo, $"{c.Parrilla} {c.Direccion}: {c.Hasta - c.Desde:0} mm"));
        Assert.Contains(r.Avisos, a => a.Contains("no caben los doblados"));
    }

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

        // La capa pegada al recubrimiento: 75, franja de 15.9 y medio diámetro.
        Assert.Equal(98.85, r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Capa == 1).Posiciones[0], 3);
        // La de encima entra en la curva del doblez: 75 + 15.9 + 95 / 2.
        Assert.Equal(138.4, r.Conjuntos.First(b => b.Parrilla == Parrilla.Inferior && b.Capa == 2).Posiciones[0], 3);
    }

    [Fact]
    public void El_doblez_de_la_capa_vecina_no_cruza_la_barra_de_borde()
    {
        // Eje del arco: a un radio R de la pata y del tramo recto, con R = D / 2 + d / 2.
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), Config(superior: false), R);
        ConjuntoBarras abajo = r.Conjuntos.First(b => b.Capa == 1), encima = r.Conjuntos.First(b => b.Capa == 2);
        double radio = 95.0 / 2 + 15.9 / 2;
        double cx = abajo.Desde + 15.9 / 2 + radio, cz = abajo.Z + radio;
        double distancia = Math.Sqrt(Math.Pow(encima.Posiciones[0] - cx, 2) + Math.Pow(encima.Z - cz, 2));

        // Dentro de la curva: el eje de la barra queda a más de un diámetro del eje del arco.
        Assert.True(radio - distancia >= 15.9 - 0.001, $"{radio - distancia}");
    }

    [Fact]
    public void Un_doblado_mayor_en_el_proyecto_aleja_la_barra_de_borde()
    {
        ConfigParrillas c = Config(numero: 3, superior: false) with { Doblados = new Dictionary<int, double> { [3] = 63.5 } };
        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), c, R);

        Assert.Equal(75 + 9.5 + 63.5 / 2, r.Conjuntos.First(b => b.Capa == 2).Posiciones[0], 3);
    }

    [Fact]
    public void Una_combinada_rectangular_da_cantidades_y_largos()
    {
        var z = new ZapataEntrada(Rect(4000, 1200), 600, [new(1000, 600), new(3000, 600)]);

        ResultadoZapata r = MotorParrillas.Calcular(z, Config(superior: false), R);

        Assert.Empty(r.Errores);
        Assert.Equal(Direccion.X, r.Principal);
        ConjuntoBarras enX = r.Conjuntos.Single(b => b.Direccion == Direccion.X), enY = r.Conjuntos.Single(b => b.Direccion == Direccion.Y);
        Assert.Equal(1, enX.Capa);
        // X: 3850 de tramo y patas de 450. Y: 1050 de tramo y patas de 434.1.
        Assert.Equal(3850 + 2 * 450, enX.LargoNeto, 3);
        Assert.Equal(1050 + 2 * 434.1, enY.LargoNeto, 3);
        // X: 1050 − 2 × 23.85 = 1002.3 → 7 espacios. Y: 3850 − 2 × 63.4 = 3723.2 → 25 espacios.
        Assert.Equal(8, enX.Cantidad);
        Assert.Equal(26, enY.Cantidad);
        Assert.Equal(34, r.Resumen.TotalBarras);
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
    public void Los_choques_ya_no_se_avisan_con_texto()
    {
        List<Punto> ele = [new(0, 0), new(3000, 0), new(3000, 1000), new(1000, 1000), new(1000, 3000), new(0, 3000)];

        Assert.DoesNotContain(MotorParrillas.Calcular(new ZapataEntrada(ele, 500, []), Config(), R).Avisos, a => a.Contains("Forma atípica"));
        Assert.DoesNotContain(MotorParrillas.Calcular(Maqueta(), Config(t: Terminacion.Gancho180), R).Avisos, a => a.Contains("cruzar"));
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

    [Fact]
    public void Por_defecto_la_pata_mide_lo_del_cuadro_si_cabe()
    {
        // La zapata de 0.80 m de Carlos (reunión del 05-nov): el B del cuadro de la #5 es 278 y caben 650.
        var z = new ZapataEntrada(Rect(1800, 1800), 800, [new(900, 900)]);
        var c = new ConfigParrillas(new ConfigDireccion(5, 150), new ConfigDireccion(5, 150), Recubrimientos.Global(75), false);

        ResultadoZapata r = MotorParrillas.Calcular(z, c, R);

        Assert.All(r.Conjuntos, b => Assert.Equal(278, b.Pata));
        Assert.All(r.Conjuntos, b => Assert.Equal(1650 + 2 * 278, b.LargoNeto, 3));
        Assert.DoesNotContain(r.Avisos, a => a.Contains("cuadro"));
    }

    [Fact]
    public void La_pata_del_cuadro_que_no_cabe_se_corta_en_el_recubrimiento_y_avisa()
    {
        var z = new ZapataEntrada(Rect(1800, 1800), 350, [new(900, 900)]);

        ResultadoZapata r = MotorParrillas.Calcular(z, Config(superior: false, largo: LargoPata.Cuadro), R);

        Assert.Equal(200, r.Conjuntos.First(b => b.Capa == 1).Pata, 3);
        Assert.Contains(r.Avisos, a => a.Contains("no cabe en la altura"));
    }

    [Fact]
    public void Hasta_el_recubrimiento_avisa_si_queda_mas_corta_que_el_cuadro()
    {
        var baja = new ZapataEntrada(Rect(1800, 1800), 350, [new(900, 900)]);

        Assert.Contains(MotorParrillas.Calcular(baja, Config(superior: false), R).Avisos, a => a.Contains("no cumple el largo del cuadro"));
        Assert.DoesNotContain(MotorParrillas.Calcular(Maqueta(), Config(superior: false), R).Avisos, a => a.Contains("no cumple"));
    }

    [Fact]
    public void En_L_la_barra_lleva_una_sola_pata_y_no_ocupa_franja_al_final()
    {
        var x = new ConfigDireccion(5, 150, Terminacion.Pata90, Patas.Inicio);
        var c = new ConfigParrillas(x, x, Recubrimientos.Global(75), false, LargoPata: LargoPata.Recubrimiento);

        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), c, R);

        ConjuntoBarras capa1 = r.Conjuntos.First(b => b.Capa == 1), capa2 = r.Conjuntos.First(b => b.Capa == 2);
        Assert.Equal(Patas.Inicio, capa1.Patas);
        Assert.Equal(1650 + 350, capa1.LargoNeto, 3);
        // La capa 1 empieza después de la franja de la otra dirección, pero al final llega hasta el recubrimiento.
        Assert.Equal(98.85, capa1.Posiciones[0], 3);
        Assert.Equal(1800 - 75 - 15.9 / 2, capa1.Posiciones[^1], 3);
        Assert.Equal(1800 - 75 - 15.9 / 2, capa2.Posiciones[^1], 3);
    }

    [Fact]
    public void Con_forma_recta_la_de_90_no_lleva_patas_ni_franja()
    {
        var x = new ConfigDireccion(5, 150, Terminacion.Pata90, Patas.Ninguna);
        var c = new ConfigParrillas(x, x, Recubrimientos.Global(75), false);

        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), c, R);

        Assert.All(r.Conjuntos, b =>
        {
            Assert.Equal(Terminacion.Recta, b.Terminacion);
            Assert.Equal(Patas.Ninguna, b.Patas);
            Assert.Equal(0, b.Pata);
            Assert.Equal(1650, b.LargoNeto, 3);
        });
        Assert.Equal(75 + 15.9 / 2, r.Conjuntos.First(b => b.Capa == 1).Posiciones[0], 3);
    }

    [Fact]
    public void La_parrilla_superior_puede_llevar_otro_gancho()
    {
        var x = new ConfigDireccion(5, 150, Superior: new Extremos(Terminacion.Gancho180, Cola: 79.4));
        var c = new ConfigParrillas(x, x, Recubrimientos.Global(75), true, LargoPata: LargoPata.Recubrimiento);

        ResultadoZapata r = MotorParrillas.Calcular(Maqueta(), c, R);

        Assert.All(r.Conjuntos.Where(b => b.Parrilla == Parrilla.Inferior), b => Assert.Equal(Terminacion.Pata90, b.Terminacion));
        Assert.All(r.Conjuntos.Where(b => b.Parrilla == Parrilla.Superior), b =>
        {
            Assert.Equal(Terminacion.Gancho180, b.Terminacion);
            Assert.Equal(0, b.Pata);
            Assert.Equal(79.4, b.Cola);
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
