using CQEING.Vigas.Calculo.Generacion;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo.Tests;

/// <summary>Los trazos y los choques, contra lo medido en Revit (H13 del plan de 2026-11-05: #5 CIM, d 15.88, D 95.25).</summary>
public class TrazosZapataTests
{
    private static readonly Reglas R = Reglas.Generales;
    private static readonly Dictionary<int, double> Doblado = new() { [5] = 95.25 };

    private static ZapataEntrada Zapata(double h) => new([new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], h, [new(900, 900)]);

    private static ConfigParrillas Config(Terminacion t = Terminacion.Pata90, double? cola = null, LargoPata largo = LargoPata.Recubrimiento, bool superior = false) =>
        new(new ConfigDireccion(5, 150, t, Cola: cola), new ConfigDireccion(5, 150, t, Cola: cola), Recubrimientos.Global(75), superior, Doblado, largo);

    private static (ResultadoZapata R, IReadOnlyList<Trazo> T) Calcular(ZapataEntrada z, ConfigParrillas c)
    {
        ResultadoZapata r = MotorParrillas.Calcular(z, c, R);
        return (r, TrazosZapata.Trazos(r.Conjuntos, R, c.Doblados));
    }

    [Fact]
    public void Hay_un_trazo_por_barra_con_su_id()
    {
        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(Zapata(500), Config(superior: true));

        Assert.Equal(r.Resumen.TotalBarras, t.Count);
        Assert.Equal("0.0", t[0].Pieza);
        Assert.Equal(5, t[0].Numero);
    }

    [Fact]
    public void La_pata_de_90_dobla_como_Revit_y_llega_a_su_punta()
    {
        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(Zapata(350), Config());
        ConjuntoBarras c = r.Conjuntos[0];
        Trazo barra = t[0];

        // La pata va por el eje: cara del recubrimiento + d/2; la punta, a 75 + 200 (decisión 16).
        Assert.Equal(Direccion.X, c.Direccion);
        Assert.Equal(75 + 15.9 / 2, barra.Puntos[0].X, 3);
        Assert.Equal(275, barra.Puntos[0].Z, 3);
        Assert.Equal(275, barra.Puntos[^1].Z, 3);
        // El tramo recto empieza D/2 + d hacia adentro (H13: 2675 → 2738.5 con el d de Revit).
        Assert.Contains(barra.Puntos, p => Math.Abs(p.X - (75 + 95.25 / 2 + 15.9)) < 1e-6 && Math.Abs(p.Z - c.Z) < 1e-6);
    }

    [Fact]
    public void El_gancho_de_180_queda_donde_lo_midio_Revit()
    {
        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(Zapata(350), Config(Terminacion.Gancho180, cola: 79.37));
        ConjuntoBarras c = r.Conjuntos[0];
        Trazo barra = t[0];
        double radio = 95.25 / 2 + 15.9 / 2;

        // H13 (#5, Estándar - 180°.): la cola vuelve hacia adentro a 2 radios de la barra y mide 79.37.
        Assert.Equal(c.Desde + 95.25 / 2 + 15.9 + 79.37, barra.Puntos[0].X, 3);
        Assert.Equal(c.Z + 2 * radio, barra.Puntos[0].Z, 3);
        Assert.Equal(c.Desde + 15.9 / 2, barra.Puntos.Min(p => p.X), 1);
    }

    [Fact]
    public void Sin_cola_de_Revit_usa_la_del_cuadro()
    {
        (_, IReadOnlyList<Trazo> t) = Calcular(Zapata(350), Config(Terminacion.Gancho180));
        double cola = R.PorDiametro[5].Gancho180.E!.Value;

        Assert.Equal(cola, t[0].Puntos[0].X - t[0].Puntos[1].X, 3);
    }

    [Fact]
    public void La_L_lleva_pata_solo_al_inicio()
    {
        var x = new ConfigDireccion(5, 150, Terminacion.Pata90, Patas.Inicio);
        ResultadoZapata r = MotorParrillas.Calcular(Zapata(350), new ConfigParrillas(x, x, Recubrimientos.Global(75), false, Doblado), R);
        Trazo barra = TrazosZapata.Trazos(r.Conjuntos, R, Doblado)[0];

        Assert.Equal(275, barra.Puntos[0].Z, 3);
        Assert.Equal(r.Conjuntos[0].Z, barra.Puntos[^1].Z, 3);
        Assert.Equal(r.Conjuntos[0].Hasta, barra.Puntos[^1].X, 3);
    }

    [Theory]
    [InlineData(LargoPata.Recubrimiento)]
    [InlineData(LargoPata.Cuadro)]
    public void Una_rectangular_con_patas_no_tiene_choques(LargoPata largo)
    {
        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(Zapata(500), Config(largo: largo, superior: true));

        Assert.Empty(TrazosZapata.Choques(r.Conjuntos, t));
    }

    [Fact]
    public void Los_ganchos_de_135_marcan_choques_con_la_barra_de_borde()
    {
        // Prueba automática de #246 en Revit: con 135° en h 350 la barra de borde y el gancho de la otra dirección chocan.
        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(Zapata(350), Config(Terminacion.Gancho135, cola: 79.37));

        IReadOnlyList<ChoqueZapata> choques = TrazosZapata.Choques(r.Conjuntos, t);

        Assert.NotEmpty(choques);
        Assert.All(choques, c => Assert.Matches(@"^La barra \d+ de la parrilla inferior en [XY] \(#5\) se mete \d+\.\d mm en la barra \d+ de la parrilla inferior en [XY] \(#5\)$", c.Texto));
    }

    [Fact]
    public void Una_L_marca_choques_en_la_esquina_entrante()
    {
        // Prueba automática de #246 en Revit (zapata 817679): L de h 500 con las dos parrillas, 14 pares, −8.9 mm.
        List<Punto> ele = [new(0, 0), new(3000, 0), new(3000, 1000), new(1000, 1000), new(1000, 3000), new(0, 3000)];

        (ResultadoZapata r, IReadOnlyList<Trazo> t) = Calcular(new ZapataEntrada(ele, 500, []), Config(superior: true));
        IReadOnlyList<ChoqueZapata> choques = TrazosZapata.Choques(r.Conjuntos, t);

        Assert.Equal(14, choques.Count);
        Assert.Equal(8.9, choques.Max(c => c.SeMeteMm), 1);
    }
}
