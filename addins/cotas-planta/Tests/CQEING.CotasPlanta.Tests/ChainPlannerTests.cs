using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;

namespace CQEING.CotasPlanta.Tests;

public class ChainPlannerTests
{
    private static readonly SideLayout LeftLayout =
        new(PlanSide.Left, new[] { ChainKind.Libres, ChainKind.Voladizo, ChainKind.Total });

    /// <summary>
    /// Planta de 1 × 2 vanos, parecida al lado izquierdo de N+3.70: ejes X = 0 y 4, ejes Y = 0, 4 y 8;
    /// columnas de 0.30 en cada cruce, dos viguetas en X (y = 2 y y = 6), una viga de borde de 10 cm
    /// por debajo del eje y = 0 (caras en −1.45 y −1.55) y una viga en Y que llega hasta −2.07.
    /// </summary>
    private static PlanGeometry Plan()
    {
        var axes = new[]
        {
            new GridAxis(1, "A", Axis2.X, 0, -3, 10),
            new GridAxis(2, "B", Axis2.X, 4, -3, 10),
            new GridAxis(3, "1", Axis2.Y, 0, -2, 6),
            new GridAxis(4, "2", Axis2.Y, 4, -2, 6),
            new GridAxis(5, "3", Axis2.Y, 8, -2, 6),
        };

        long id = 100;
        var columns = (from x in new[] { 0.0, 4.0 }
                       from y in new[] { 0.0, 4.0, 8.0 }
                       select new ColumnBox(id++, new Rect2(x - 0.15, y - 0.15, x + 0.15, y + 0.15))).ToList();

        var beams = new[]
        {
            new BeamSegment(200, Axis2.X, new Rect2(0, 1.95, 4, 2.05), "0.10 x 0.35", BeamRole.Vigueta),
            new BeamSegment(201, Axis2.X, new Rect2(0, 5.95, 4, 6.05), "0.10 x 0.35", BeamRole.Vigueta),
            new BeamSegment(202, Axis2.X, new Rect2(0, -1.55, 4, -1.45), "0.10 x 0.35", BeamRole.Borde),
            new BeamSegment(203, Axis2.Y, new Rect2(-0.125, -2.07, 0.125, 8.15), "0.25 x 0.35", BeamRole.Viga),
        };

        return PlanGeometry.Create(axes, columns, beams, Array.Empty<SlabOutline>(), 0.05);
    }

    /// <summary>Con colores: cada cadena sigue siendo una cota aparte (sirve para ver en qué línea cayó).</summary>
    private static IReadOnlyList<ChainSpec> PlanLeft() =>
        ChainPlanner.Plan(Plan(), new CotasSettings { Sides = new[] { LeftLayout }, ColoredChains = true });

    /// <summary>En negro (lo normal): las cadenas seguidas en una línea se unen en una sola cota.</summary>
    private static IReadOnlyList<ChainSpec> PlanLeftBlack() =>
        ChainPlanner.Plan(Plan(), new CotasSettings { Sides = new[] { LeftLayout } });

    private static ChainSpec Chain(ChainKind kind, int ring, double from, double to) =>
        new(PlanSide.Left, kind, ring, new[]
        {
            new ChainRef(from, RefKind.Axis, 1),
            new ChainRef(to, RefKind.Axis, 2),
        });

    private static List<ChainSpec> Cantilevers(IEnumerable<ChainSpec> chains) =>
        chains.Where(c => c.Kind == ChainKind.Voladizo).OrderBy(c => c.To - c.From).ToList();

    private static ChainSpec CantileverOf(IEnumerable<ChainSpec> chains, double length) =>
        Assert.Single(Cantilevers(chains), c => Math.Abs(c.To - c.From - length) < 0.001);

    [Fact]
    public void Cada_voladizo_se_mide_completo_desde_el_eje()
    {
        List<ChainSpec> cantilevers = Cantilevers(PlanLeft());

        // Cara interior (1.45) y exterior (1.55) de la viga de borde y extremo de la viga (2.07), no 0.52.
        Assert.Equal(new[] { 1.45, 1.55, 2.07 }, cantilevers.Select(c => Math.Round(c.To - c.From, 2)));
        Assert.All(cantilevers, c => Assert.Equal(2, c.Refs.Count));
        Assert.All(cantilevers, c => Assert.Contains(c.Refs, r => r.Kind == RefKind.Axis));
    }

    [Fact]
    public void La_cara_exterior_sigue_en_la_linea_de_viguetas()
    {
        IReadOnlyList<ChainSpec> chains = PlanLeft();

        Assert.Equal(1, Assert.Single(chains, c => c.Kind == ChainKind.Viguetas).Ring);
        ChainSpec outer = CantileverOf(chains, 1.55);
        Assert.Equal(1, outer.Ring);
        Assert.Null(outer.TextAt);
    }

    [Fact]
    public void La_cara_interior_comparte_linea_con_el_extremo_con_los_textos_lado_a_lado()
    {
        IReadOnlyList<ChainSpec> chains = PlanLeft();

        ChainSpec extreme = CantileverOf(chains, 2.07);
        ChainSpec inner = CantileverOf(chains, 1.45);
        Assert.Equal(2, extreme.Ring);
        Assert.Equal(extreme.Ring, inner.Ring);
        // Cada texto centrado en su cota; el del 2.07 un renglón más afuera.
        Assert.Null(inner.TextAt);
        Assert.Equal(0, inner.TextRow);
        Assert.Null(extreme.TextAt);
        Assert.Equal(1, extreme.TextRow);
    }

    [Fact]
    public void Un_tercer_voladizo_del_mismo_eje_no_cabe_en_la_linea()
    {
        var placed = new List<ChainSpec>
        {
            Chain(ChainKind.Voladizo, 0, 0, 2.07),
            Chain(ChainKind.Voladizo, 0, 0, 1.35),
        };

        Assert.Equal(1, ChainPlanner.Place(placed, Chain(ChainKind.Voladizo, 0, 0, 1.70)).Ring);
    }

    [Fact]
    public void En_la_linea_de_viguetas_no_se_monta_ningun_voladizo()
    {
        var placed = new List<ChainSpec>
        {
            Chain(ChainKind.Libres, 0, -0.15, 8.15),
            Chain(ChainKind.Viguetas, 1, 1.0, 8),
            Chain(ChainKind.Voladizo, 1, 0, 1.0),
        };

        ChainSpec placedChain = ChainPlanner.Place(placed, Chain(ChainKind.Voladizo, 0, 0, 0.8));

        // La línea 1 lleva viguetas (se unirá en una sola cota): el voladizo se va a la siguiente.
        Assert.Equal(2, placedChain.Ring);
    }

    [Fact]
    public void En_negro_los_voladizos_montados_quedan_como_dos_cotas()
    {
        IReadOnlyList<ChainSpec> chains = PlanLeftBlack();

        List<ChainSpec> line = chains.Where(c => c.Ring == 2).ToList();
        Assert.Equal(2, line.Count);
        Assert.Single(line, c => c.TextRow == 1);
    }

    [Fact]
    public void Las_libres_van_en_la_primera_linea_y_el_total_en_la_ultima()
    {
        IReadOnlyList<ChainSpec> chains = PlanLeft();

        Assert.Equal(0, Assert.Single(chains, c => c.Kind == ChainKind.Libres).Ring);
        ChainSpec total = Assert.Single(chains, c => c.Kind == ChainKind.Total);
        Assert.Equal(chains.Max(c => c.Ring), total.Ring);
        Assert.Single(chains, c => c.Ring == total.Ring);
    }

    [Fact]
    public void El_lado_queda_en_cuatro_lineas()
    {
        // Libres; viguetas + voladizo de 1.55; voladizo de 2.07; total.
        Assert.Equal(4, SideInfo.RingCount(PlanLeft(), PlanSide.Left));
        Assert.Equal(4, SideInfo.RingCount(PlanLeftBlack(), PlanSide.Left));
    }

    [Fact]
    public void En_negro_el_voladizo_y_las_viguetas_son_una_sola_cota()
    {
        IReadOnlyList<ChainSpec> chains = PlanLeftBlack();

        ChainSpec line = Assert.Single(chains, c => c.Ring == 1);
        Assert.Equal(ChainKind.Viguetas, line.Kind);
        Assert.Equal(-1.55, line.From, 3);
        Assert.Equal(8, line.To, 3);
        // El eje 1 (y = 0) se reutiliza: aparece una sola vez.
        Assert.Single(line.Refs, r => Math.Abs(r.Position) < 0.001);
    }

    [Fact]
    public void Cadenas_separadas_en_la_misma_linea_no_se_unen()
    {
        var chains = new List<ChainSpec>
        {
            Chain(ChainKind.Voladizo, 0, -1.45, 0),
            Chain(ChainKind.Voladizo, 0, 8, 9.55),
        };

        Assert.Equal(2, ChainPlanner.JoinLines(chains).Count);
    }

    [Fact]
    public void Tres_cadenas_seguidas_se_unen_en_una()
    {
        var chains = new List<ChainSpec>
        {
            Chain(ChainKind.EjeAEje, 0, 0, 8),
            Chain(ChainKind.Voladizo, 0, -1.45, 0),
            Chain(ChainKind.Voladizo, 0, 8, 9.55),
        };

        ChainSpec line = Assert.Single(ChainPlanner.JoinLines(chains));

        Assert.Equal(ChainKind.EjeAEje, line.Kind);
        Assert.Equal(4, line.Refs.Count);
        Assert.Equal(3, line.SegmentCount);
    }

    [Fact]
    public void Una_cadena_que_solo_toca_en_la_punta_comparte_linea()
    {
        var placed = new List<ChainSpec> { Chain(ChainKind.EjeAEje, 0, 0, 8) };

        Assert.Equal(0, ChainPlanner.Place(placed, Chain(ChainKind.Voladizo, 0, -1.45, 0)).Ring);
        Assert.Equal(0, ChainPlanner.Place(placed, Chain(ChainKind.Voladizo, 0, 8, 9.2)).Ring);
    }

    [Fact]
    public void Una_cadena_que_se_monta_va_en_otra_linea()
    {
        var placed = new List<ChainSpec> { Chain(ChainKind.Libres, 0, -0.15, 8.15) };

        Assert.Equal(1, ChainPlanner.Place(placed, Chain(ChainKind.Voladizo, 0, -1.45, 0)).Ring);
    }

    [Fact]
    public void El_total_va_afuera_aunque_haya_espacio_en_otra_linea()
    {
        var placed = new List<ChainSpec> { Chain(ChainKind.EjeAEje, 0, 0, 4) };

        Assert.Equal(1, ChainPlanner.Place(placed, Chain(ChainKind.Total, 0, 5, 9)).Ring);
    }

    [Fact]
    public void Las_cotas_van_en_negro_salvo_que_se_pidan_colores()
    {
        Assert.Equal(Palette.Ink, new CotasSettings().DimensionColor(ChainKind.EjeAEje));
        Assert.Equal(
            Palette.ChainColor(ChainKind.EjeAEje),
            new CotasSettings { ColoredChains = true }.DimensionColor(ChainKind.EjeAEje));
    }

    [Fact]
    public void Sin_colores_la_leyenda_no_explica_las_cadenas()
    {
        PlanGeometry geometry = Plan();
        var settings = new CotasSettings { Sides = new[] { LeftLayout } };
        IReadOnlyList<ChainSpec> chains = ChainPlanner.Plan(geometry, settings);

        Assert.DoesNotContain(LegendContent.Build(geometry, chains, settings), e => e.Symbol == LegendSymbol.Chain);
        Assert.Contains(
            LegendContent.Build(geometry, chains, settings with { ColoredChains = true }),
            e => e.Symbol == LegendSymbol.Chain);
    }
}
