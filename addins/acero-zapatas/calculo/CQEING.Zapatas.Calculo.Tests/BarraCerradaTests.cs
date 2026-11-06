using CQEING.Vigas.Calculo.Generacion;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo.Tests;

/// <summary>
/// La barra cerrada en el corte (decisión 43 del plan de 2026-11-05), contra lo medido en Revit en H21: zapata de
/// 1.80 × 1.80 × 0.35, #5 CIM con doblez de estribo de 63.5 mm y 75 mm de recubrimiento en todo el contorno.
/// </summary>
public class BarraCerradaTests
{
    private static readonly Reglas R = Reglas.Generales;
    private static readonly Dictionary<int, double> Estribo = new() { [4] = 50.8, [5] = 63.5 };

    private static ZapataEntrada Zapata(double h = 350) => new([new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], h, [new(900, 900)]);

    private static ConfigParrillas Config(Terminacion x, Terminacion y, bool superior = true) =>
        new(new ConfigDireccion(5, 150, x), new ConfigDireccion(4, 150, y), Recubrimientos.Global(75), superior, DobladosEstribo: Estribo);

    [Fact]
    public void La_cerrada_va_de_recubrimiento_a_recubrimiento_y_reemplaza_a_la_superior_de_su_direccion()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(), Config(Terminacion.Cerrada, Terminacion.Pata90), R);

        Assert.Empty(r.Errores);
        ConjuntoBarras x = Assert.Single(r.Conjuntos, b => b.Direccion == Direccion.X);
        Assert.Equal(Parrilla.Inferior, x.Parrilla);
        Assert.Equal(Terminacion.Cerrada, x.Terminacion);
        Assert.Equal(75 + 15.9 / 2, x.Z, 2);
        Assert.Equal(350 - 75 - 15.9 / 2, x.ZArriba!.Value, 2);
        Assert.Equal(200, x.Pata, 2);
        Assert.Equal(75, x.Desde, 2);
        Assert.Equal(1725, x.Hasta, 2);
        Assert.Equal(12, x.Cantidad);
        // La Y sigue con sus dos parrillas: la de arriba pasa por debajo del tramo de arriba de la cerrada.
        ConjuntoBarras ySup = Assert.Single(r.Conjuntos, b => b.Direccion == Direccion.Y && b.Parrilla == Parrilla.Superior);
        Assert.Equal(350 - 75 - 15.9 - 12.7 / 2, ySup.Z, 2);
    }

    [Fact]
    public void Con_las_dos_cerradas_la_Y_queda_adentro_de_la_X_y_entra_en_su_doblez()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(), Config(Terminacion.Cerrada, Terminacion.Cerrada), R);

        Assert.Empty(r.Errores);
        Assert.Equal(2, r.Conjuntos.Count);
        ConjuntoBarras y = Assert.Single(r.Conjuntos, b => b.Direccion == Direccion.Y);
        Assert.Equal(75 + 15.9 + 12.7 / 2, y.Z, 2);
        Assert.Equal(350 - 75 - 15.9 - 12.7 / 2, y.ZArriba!.Value, 2);
        // Como la U (decisión 26): recubrimiento + d de la X + D de estribo / 2.
        Assert.Equal(75 + 15.9 + 63.5 / 2, y.Posiciones[0], 2);
    }

    [Fact]
    public void Sin_parrilla_superior_la_cerrada_igual_necesita_altura_para_dos_capas()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(h: 170), Config(Terminacion.Cerrada, Terminacion.Pata90, superior: false), R);

        Assert.NotEmpty(r.Errores);
    }

    [Fact]
    public void El_largo_suma_las_cuatro_caras_y_los_dos_ganchos_de_135()
    {
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(), Config(Terminacion.Cerrada, Terminacion.Pata90), R);

        ConjuntoBarras x = r.Conjuntos.First(b => b.Direccion == Direccion.X);
        int gancho = Reglas.LargoGancho(R.PorDiametro[5], TipoGancho.De135);
        Assert.Equal(2 * 1650 + 2 * 200 + 2 * gancho, x.LargoNeto, 2);
    }

    [Fact]
    public void El_trazo_da_la_vuelta_sin_salir_del_recubrimiento_y_cierra_arriba_al_inicio()
    {
        ConfigParrillas c = Config(Terminacion.Cerrada, Terminacion.Pata90);
        c = c with { X = c.X with { Cola = 79.4 } };
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(), c, R);
        int k = r.Conjuntos.ToList().FindIndex(b => b.Direccion == Direccion.X);
        Trazo t = TrazosZapata.Trazos(r.Conjuntos, R, null, c.DobladosEstribo).First(x => x.Pieza == TrazosZapata.Pieza(k, 0));

        double d = 15.9;
        Assert.All(t.Puntos, p =>
        {
            Assert.InRange(p.X, 75 + d / 2 - 0.01, 1725 - d / 2 + 0.01);
            Assert.InRange(p.Z, 75 + d / 2 - 0.01, 275 - d / 2 + 0.01);
        });
        // Pasa por las cuatro caras.
        Assert.Contains(t.Puntos, p => Math.Abs(p.Z - (75 + d / 2)) < 0.01);
        Assert.Contains(t.Puntos, p => Math.Abs(p.Z - (275 - d / 2)) < 0.01);
        Assert.Contains(t.Puntos, p => Math.Abs(p.X - (75 + d / 2)) < 0.01);
        Assert.Contains(t.Puntos, p => Math.Abs(p.X - (1725 - d / 2)) < 0.01);
        // Las puntas de las dos colas, donde las dejó Revit en H21 (zapata con x desde 2600 y base en −1000):
        // (2758.6, −856.8) y (2806.8, −808.6).
        Assert.Equal(158.6, t.Puntos[0].X, 0.5);
        Assert.Equal(143.2, t.Puntos[0].Z, 0.5);
        Assert.Equal(206.8, t.Puntos[^1].X, 0.5);
        Assert.Equal(191.4, t.Puntos[^1].Z, 0.5);
    }

    [Fact]
    public void Los_ganchos_de_la_cerrada_no_cuentan_como_choque_consigo_misma()
    {
        ConfigParrillas c = Config(Terminacion.Cerrada, Terminacion.Cerrada);
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(), c, R);
        IReadOnlyList<ChoqueZapata> choques = TrazosZapata.Choques(r.Conjuntos, TrazosZapata.Trazos(r.Conjuntos, R, null, c.DobladosEstribo));

        Assert.All(choques, ch => Assert.NotEqual(ch.A, ch.B));
    }

    [Fact]
    public void El_tablero_manda_la_cerrada_y_ConTipos_le_pone_la_cola_de_estribo()
    {
        string json = """{"tipo":"aplicar","ids":["1"],"x":{"numero":5,"separacion":150,"terminacion":"Cerrada"},"y":{"numero":4,"separacion":150,"terminacion":"Pata90"},"formaX":"395791"}""";
        var a = Assert.IsType<AplicarZapatas>(ProtocoloZapatas.Leer(json));
        Assert.Equal(Terminacion.Cerrada, a.Config.X.Terminacion);

        var catalogo = new CatalogoZapatas(
            [new BarraCatalogo(5, "#5 CIM", 15.9, 95.25, 278, 189, 238, DobladoEstribo: 63.5, ColaEstribo: 79.4)],
            [new FormaCatalogo("395791", "T1", [], ClaseForma.Cerrada)], []);
        ConfigParrillas c = ProtocoloZapatas.ConTipos(a.Config, a.Tipos, catalogo);

        Assert.Equal(Terminacion.Cerrada, c.X.Terminacion);
        Assert.Equal(79.4, c.X.Cola);
        Assert.Equal(63.5, c.DobladosEstribo![5]);
    }
}
