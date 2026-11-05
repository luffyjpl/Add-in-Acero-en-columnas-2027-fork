using CQEING.Viguetas.Calculo;

namespace CQEING.Viguetas.Calculo.Tests;

/// <summary>«Crear viguetas» (#947): reparto del Excel, paneles del piso y viguetas de un panel.</summary>
public class ViguetasTests
{
    [Theory]
    // Los cinco casos del Excel de Carlos y de la reunión del 24-oct (espacio libre, cantidad, libre en extremos).
    [InlineData(2000, 2, 550)]
    [InlineData(3570, 4, 535)]
    [InlineData(2840, 3, 570)]
    [InlineData(2660, 3, 480)]
    [InlineData(3120, 4, 310)]
    public void ElRepartoDaLoMismoQueElExcel(double libre, int cantidad, double extremos)
    {
        Reparto r = RepartoViguetas.Calcular(libre, 800, 100)!;

        Assert.Equal(cantidad, r.Cantidad);
        Assert.Equal(extremos, r.LibreExtremos, 6);
        Assert.Equal(700, r.LibreMaximo, 6);
    }

    [Theory]
    [InlineData(0, 800, 100)]
    [InlineData(2000, 0, 100)]
    [InlineData(2000, 800, 800)]
    public void ConDatosQueNoSirvenNoHayReparto(double libre, double eje, double ancho) =>
        Assert.Null(RepartoViguetas.Calcular(libre, eje, ancho));

    /// <summary>Dos paneles de 4,00 × 3,00 m entre ejes, con vigas de 30 cm.</summary>
    private static List<BordePanel> Grilla() =>
    [
        new("abajo", new(0, 0), new(8000, 0), 300),
        new("arriba", new(0, 3000), new(8000, 3000), 300),
        new("A", new(0, 0), new(0, 3000), 300),
        new("B", new(4000, 0), new(4000, 3000), 300),
        new("C", new(8000, 0), new(8000, 3000), 300),
    ];

    [Fact]
    public void UnaGrillaDeDosPorUnoDaDosPanelesConSuContornoLibre()
    {
        List<Panel> paneles = DetectorPaneles.Detectar(Grilla());

        Assert.Equal(2, paneles.Count);
        Panel izquierdo = paneles.Single(p => Geometria.Centro(p.Libre).X < 4000);
        Assert.Equal(3700 * 2700, izquierdo.AreaLibre, 0);
        Assert.Equal(150, izquierdo.Libre.Min(q => q.X), 3);
        Assert.Equal(3850, izquierdo.Libre.Max(q => q.X), 3);
        Assert.Equal(4, izquierdo.Eje.Count);
    }

    [Fact]
    public void UnVoladizoNoCierraNingunPanel()
    {
        List<BordePanel> bordes = Grilla();
        bordes.Add(new("voladizo", new(8000, 0), new(9500, 0), 300));

        Assert.Equal(2, DetectorPaneles.Detectar(bordes).Count);
    }

    [Fact]
    public void UnaVigaQueTerminaEnLaCaraDeLaOtraIgualCierraElPanel()
    {
        List<BordePanel> bordes = Grilla();
        bordes[3] = new("B", new(4000, 150), new(4000, 2850), 300);

        Assert.Equal(2, DetectorPaneles.Detectar(bordes).Count);
    }

    [Fact]
    public void LasViguetasExistentesSoloPartenElPanelSiSePide()
    {
        List<ArmazonPiso> armazon = Grilla().Select(b => new ArmazonPiso(b.Id, b.Desde, b.Hasta, b.Ancho, "V30X40", false)).ToList();
        armazon.Add(new("vt", new(2000, 0), new(2000, 3000), 100, "VT_10X35", false));
        armazon.Add(new("creada", new(6000, 0), new(6000, 3000), 100, "VT_10X35", true));
        var piso = new PisoLeido("1", "N+3.70", 3700, armazon, []);

        var soloVigas = new PisoViguetas(piso, viguetasComoBorde: false);
        Assert.Equal(2, soloVigas.Paneles.Count);
        Assert.Single(soloVigas.CreadasEn(soloVigas.Paneles.Single(p => Geometria.Centro(p.Libre).X > 4000)));
        // La creada por el add-in nunca parte el panel; la vigueta del modelo sí, si se pide.
        Assert.Equal(3, new PisoViguetas(piso, viguetasComoBorde: true).Paneles.Count);
    }

    /// <summary>Panel libre de 3,57 × 2,00 m (los de la maqueta) entre vigas de 30 cm.</summary>
    private static Panel PanelMaqueta()
    {
        List<BordePanel> bordes =
        [
            new("abajo", new(0, 0), new(3870, 0), 300),
            new("arriba", new(0, 2300), new(3870, 2300), 300),
            new("izq", new(0, 0), new(0, 2300), 300),
            new("der", new(3870, 0), new(3870, 2300), 300),
        ];
        return DetectorPaneles.Detectar(bordes).Single();
    }

    [Fact]
    public void LasViguetasEnXSeRepartenEnLosDosMetrosYVanDeEjeAEje()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string>())!;

        RepartoPanel r = Assert.Single(p.Paneles);
        Assert.Equal(2000, r.Libre, 3);
        Assert.Equal(2, r.Reparto.Cantidad);
        Assert.Equal(550, r.Reparto.LibreExtremos, 3);
        Assert.Equal(["P1:1", "P1:2"], p.Viguetas.Select(v => v.Id));
        Assert.Equal([750.0, 1550.0], p.Viguetas.Select(v => Math.Round(v.Cota, 3)));
        Vigueta primera = p.Viguetas[0];
        Assert.Equal(3570, primera.Largo, 3);
        Assert.Equal(0, primera.EjeDesde, 3);
        Assert.Equal(3870, primera.EjeHasta, 3);
        Assert.Equal(new TipoVigueta(1, 3570, 2), Assert.Single(p.Tipos));
    }

    [Fact]
    public void EnYSonCuatroYUnaBorradaNoCuentaEnLosTipos()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.Y, 800, 100, new HashSet<string> { "P1:2" })!;

        Assert.Equal(4, p.Paneles[0].Reparto.Cantidad);
        Assert.True(p.Viguetas.Single(v => v.Id == "P1:2").Borrada);
        Assert.Equal(3, p.Vivas.Count());
        Assert.Equal(3, Assert.Single(p.Tipos).Cantidad);
    }

    [Fact]
    public void EnUnPanelEnLLasViguetasQueCruzanElHuecoSePartenEnDos()
    {
        // Una L: 6 × 4 m menos la esquina de arriba a la derecha (3 × 2 m), por ejes.
        List<BordePanel> bordes =
        [
            new("1", new(0, 0), new(6000, 0), 200),
            new("2", new(6000, 0), new(6000, 2000), 200),
            new("3", new(6000, 2000), new(3000, 2000), 200),
            new("4", new(3000, 2000), new(3000, 4000), 200),
            new("5", new(3000, 4000), new(0, 4000), 200),
            new("6", new(0, 4000), new(0, 0), 200),
        ];
        Panel l = DetectorPaneles.Detectar(bordes).Single();

        PropuestaViguetas p = PlanificadorViguetas.Planificar([l], Direccion.Y, 800, 100, new HashSet<string>())!;

        Assert.All(p.Viguetas, v => Assert.InRange(v.Largo, 1000, 3900));
        Assert.Contains(p.Viguetas, v => v.Cota > 3100 && v.Largo < 2000);
        Assert.True(p.Tipos.Count >= 2);
    }

    /// <summary>Una viga corta que entra al panel desde abajo hasta 1,20 m, en x = 2,00 m (como la de P4 en la revisión).</summary>
    private static readonly BordePanel VigaQueCruza = new("corta", new(2000, 0), new(2000, 1200), 200);

    [Fact]
    public void UnaVigaQueCruzaParteLaViguetaEnDosLados()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string>(), [VigaQueCruza])!;

        // Reparto parejo con una vigueta en la punta de la viga corta, por dentro (cara en y = 1,20 m, eje en 1,15 m, justo
        // en el centro del libre): 3 a 0,80 m con extremos de 0,15 m (decisión 31). La 1 cruza la viga corta; la 2 se
        // apoya en su punta y la 3 pasa por encima.
        Assert.Equal(["P1:1a", "P1:1b", "P1:2a", "P1:2b", "P1:3"], p.Viguetas.Select(v => v.Id));
        Assert.Equal([350.0, 350.0, 1150.0, 1150.0, 1950.0], p.Viguetas.Select(v => Math.Round(v.Cota, 3)));
        Vigueta a = p.Viguetas[0], b = p.Viguetas[1];
        Assert.Equal((150.0, 1900.0, 0.0, 2000.0), (a.Desde, a.Hasta, a.EjeDesde, a.EjeHasta));
        Assert.Equal((2100.0, 3720.0, 2000.0, 3870.0), (b.Desde, b.Hasta, b.EjeDesde, b.EjeHasta));
        // La viga corta termina dentro del panel (decisión 30): cada lado llega a su eje y no se unen, también al crearlas
        // todas de nuevo. La de la punta se parte en piezas, pero sin tope: se crea de punta a punta.
        Assert.True(a.TopeHasta && b.TopeDesde);
        Assert.All(p.Viguetas.Where(v => v.Panel == "P1" && Math.Round(v.Cota) == 1150), v => Assert.False(v.TopeDesde || v.TopeHasta));
        Assert.Equal(4, p.Lineas(continuas: true).Count);
        Assert.Equal(5, p.Lineas(continuas: false).Count);
    }

    [Fact]
    public void QuitarUnLadoDeLaViguetaDeLaPuntaDejaElOtroHastaElEje()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string> { "P1:2a" }, [VigaQueCruza])!;

        (Punto desde, Punto hasta) = p.Lineas(continuas: true).Single(l => Math.Round(l.Desde.Y) == 1150);
        Assert.Equal((2000.0, 3870.0), (desde.X, hasta.X));
    }

    [Fact]
    public void LaViguetaEnLaPuntaDeLaVigaCortaCorrePorTodaLaColumna()
    {
        // Una columna de dos paneles con el libre de P4 (3,48 m: 3,78 m entre ejes, vigas de 30 cm); en el de abajo entra
        // una viga corta desde la derecha hasta x = 1,15 m (el eje de la vigueta de la punta queda a 1,05 m del libre).
        List<BordePanel> bordes =
        [
            new("abajo", new(0, 0), new(3780, 0), 300),
            new("medio", new(0, 3000), new(3780, 3000), 300),
            new("arriba", new(0, 6000), new(3780, 6000), 300),
            new("izq", new(0, 0), new(0, 6000), 300),
            new("der", new(3780, 0), new(3780, 6000), 300),
            new("corta", new(3780, 1500), new(1150, 1500), 200),
        ];
        List<Panel> paneles = DetectorPaneles.Detectar(bordes);
        Assert.Equal(2, paneles.Count);

        PropuestaViguetas p = PlanificadorViguetas.Planificar(paneles, Direccion.Y, 800, 100, new HashSet<string>(), bordes)!;

        // Como en el modelo de referencia: 5 parejas a 0,69 m con extremos de 0,31 m (la fórmula daba 4), y la segunda en
        // la punta, por dentro de la viga (cara al ras de 1,15 m, eje en 1,20 m), en los dos paneles.
        Assert.All(p.Paneles, r => Assert.Equal([510.0, 1200.0, 1890.0, 2580.0, 3270.0],
            p.Viguetas.Where(v => v.Panel == r.Panel).Select(v => Math.Round(v.Cota, 3)).Distinct()));
        RepartoPanel primero = p.Paneles[0];
        Assert.Equal((5, 690.0, 310.0, 310.0), (primero.Reparto.Cantidad, primero.Reparto.EjeAEje, Math.Round(primero.Reparto.LibreExtremos, 3),
            Math.Round(primero.LibreOtroExtremo, 3)));
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        List<(Punto Desde, Punto Hasta)> EnX(double x) => lineas.Where(l => Math.Abs(l.Desde.X - x) < 1).ToList();
        // La de la punta va de punta a punta de la columna; las que cruzan la viga corta paran en ella.
        (Punto desde, Punto hasta) = Assert.Single(EnX(1200));
        Assert.Equal((0.0, 6000.0), (desde.Y, hasta.Y));
        Assert.Equal(2, EnX(1890).Count);
        Assert.Single(EnX(510));
    }

    [Fact]
    public void SiElRepartoYaCaeEnLaPuntaNoSeSumaOtra()
    {
        // Viga corta hasta y = 1,60 m: la de la punta (eje 1,55 m) es la 2 del reparto de 0,80 m.
        var corta = new BordePanel("corta", new(2000, 0), new(2000, 1600), 200);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string>(), [corta])!;

        Assert.Equal([750.0, 1550.0], p.Viguetas.Select(v => Math.Round(v.Cota, 3)).Distinct());
    }

    [Fact]
    public void LaVigaQueCruzaElPanelDeLadoALadoNoParaLasViguetas()
    {
        // La misma viga, pero de borde a borde del panel: las viguetas la atraviesan y se crean de punta a punta.
        var deLadoALado = new BordePanel("entera", new(2000, 0), new(2000, 2300), 200);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string>(), [deLadoALado])!;

        Assert.All(p.Viguetas, v => Assert.False(v.TopeDesde || v.TopeHasta));
        Assert.Equal(4, p.Viguetas.Count);
        Assert.Equal(2, p.Lineas(continuas: true).Count);
    }

    [Fact]
    public void BorrarUnLadoDejaLaViguetaHastaLaViga()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string> { "P1:1b" }, [VigaQueCruza])!;

        (Punto desde, Punto hasta) = p.Lineas(continuas: true).Single(l => Math.Round(l.Desde.Y) == 350);
        Assert.Equal((0.0, 2000.0), (desde.X, hasta.X));
    }

    [Fact]
    public void DosPanelesSeguidosDanViguetasContinuasDePuntaAPunta()
    {
        List<Panel> paneles = DetectorPaneles.Detectar(Grilla());

        PropuestaViguetas p = PlanificadorViguetas.Planificar(paneles, Direccion.X, 800, 100, new HashSet<string>(), Grilla())!;

        Assert.Equal(2, p.Paneles.Count);
        Assert.All(p.Paneles, r => Assert.Equal(3, r.Reparto.Cantidad));
        List<(Punto Desde, Punto Hasta)> continuas = p.Lineas(continuas: true);
        Assert.Equal(3, continuas.Count);
        Assert.All(continuas, l => Assert.Equal((0.0, 8000.0), (Math.Round(l.Desde.X), Math.Round(l.Hasta.X))));
        Assert.Equal(6, p.Lineas(continuas: false).Count);
        // Los tipos se agrupan entre los dos paneles: seis piezas iguales, un tipo.
        Assert.Equal(new TipoVigueta(1, 3700, 6), Assert.Single(p.Tipos));
    }

    /// <summary>Columna de dos paneles: el de arriba de 4,00 × 3,00 m y el de abajo un poco más angosto (3,40 m).</summary>
    private static List<Panel> ColumnaDistinta() => DetectorPaneles.Detectar(
    [
        new("arriba", new(0, 6000), new(4000, 6000), 300),
        new("medio", new(0, 3000), new(4000, 3000), 300),
        new("abajo", new(0, 0), new(3400, 0), 300),
        new("izq", new(0, 0), new(0, 6000), 300),
        new("der", new(4000, 3000), new(4000, 6000), 300),
        new("der2", new(3400, 0), new(3400, 3000), 300),
    ]);

    [Fact]
    public void LosPanelesDeUnaColumnaUsanLasLineasDelPrimero()
    {
        List<Panel> columna = ColumnaDistinta();
        Panel arriba = columna.Single(p => Geometria.Centro(p.Libre).Y > 3000), abajo = columna.Single(p => p != arriba);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([arriba, abajo], Direccion.Y, 800, 100, new HashSet<string>())!;

        RepartoPanel r = p.Paneles.Single(x => x.Panel == abajo.Id);
        Assert.Equal(OrigenReparto.Alineado, r.Origen);
        Assert.Equal(arriba.Id, r.Guia);
        List<double> cotasAbajo = p.Viguetas.Where(v => v.Panel == abajo.Id).Select(v => Math.Round(v.Cota)).ToList();
        List<double> cotasArriba = p.Viguetas.Where(v => v.Panel == arriba.Id).Select(v => Math.Round(v.Cota)).ToList();
        // Las de abajo están todas en líneas de las de arriba: son continuas.
        Assert.NotEmpty(cotasAbajo);
        Assert.All(cotasAbajo, c => Assert.Contains(c, cotasArriba));
        Assert.True(p.Lineas(continuas: true).Count < p.Lineas(continuas: false).Count);
    }

    [Fact]
    public void ConViguetasExistentesEnLaColumnaLasNuevasLasSiguen()
    {
        List<Panel> columna = ColumnaDistinta();
        Panel abajo = columna.Single(p => Geometria.Centro(p.Libre).Y < 3000);
        List<ArmazonPiso> armazon = [new("vt1", new(1000, 3000), new(1000, 6000), 100, "VT_10X35", false), new("vt2", new(2200, 3000), new(2200, 6000), 100, "VT_10X35", false)];
        var piso = new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);

        List<double> guias = piso.Guias(Direccion.Y);
        PropuestaViguetas p = PlanificadorViguetas.Planificar([abajo], Direccion.Y, 800, 100, new HashSet<string>(), guias: guias)!;

        Assert.Equal([1000.0, 2200.0], guias);
        RepartoPanel r = Assert.Single(p.Paneles);
        Assert.Equal(OrigenReparto.Existentes, r.Origen);
        Assert.Equal([1000.0, 2200.0], p.Viguetas.Select(v => v.Cota));
        Assert.Equal(1200, r.Reparto.EjeAEje);
        // Una vigueta horizontal no es guía de viguetas en ↕ Y.
        Assert.Empty(piso.Guias(Direccion.X));
    }

    [Fact]
    public void LasViguetasDelModeloSonGuiaYLasVigasNoSonPiezas()
    {
        List<Panel> columna = ColumnaDistinta();
        Panel arriba = columna.Single(p => Geometria.Centro(p.Libre).Y > 3000), abajo = columna.Single(p => p != arriba);
        List<ArmazonPiso> armazon =
        [
            new("vt1", new(1000, 3000), new(1000, 6000), 100, "VT_10X35", false),
            new("vt2", new(2200, 3000), new(2200, 6000), 100, "VT_10X35", false),
            new("viga", new(0, 3000), new(4000, 3000), 300, "V30X40", false),
        ];
        var piso = new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);

        // Las de adentro de lo elegido también son guía (decisión 28).
        Assert.Equal([1000.0, 2200.0], piso.Guias(Direccion.Y));
        Assert.Equal(["vt1#1", "vt2#1"], piso.PiezasDelModelo([arriba]).Select(x => x.Id));
        Assert.Equal("vt1", PisoViguetas.ElementoDe("vt1#1"));
    }

    [Fact]
    public void UnaViguetaDelModeloSeQuitaPorPiezasDeCadaLadoDeLaViga()
    {
        // Columna de tres paneles de 4,00 × 3,00 m, una vigueta del modelo que la cruza entera en x = 1,00 m y una viga
        // corta que la cruza en el panel del medio, a 4,50 m (como la de P4).
        List<BordePanel> bordes = [new("izq", new(0, 0), new(0, 9000), 300), new("der", new(4000, 0), new(4000, 9000), 300)];
        bordes.AddRange(new[] { 0, 3000, 6000, 9000 }.Select(y => new BordePanel($"h{y}", new(0, y), new(4000, y), 300)));
        var vigueta = new ArmazonPiso("vt", new(1000, 0), new(1000, 9000), 100, "VT_10X35", false);
        List<ArmazonPiso> armazon = bordes.Select(b => new ArmazonPiso(b.Id, b.Desde, b.Hasta, b.Ancho, "V30X40", false))
            .Append(vigueta).Append(new ArmazonPiso("corta", new(0, 4500), new(2000, 4500), 200, "V20X30", false)).ToList();
        var piso = new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);
        Panel medio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        // En el panel del medio, la viga corta la parte en dos piezas: de 3,00 a 4,50 m y de 4,50 a 6,00 m.
        List<PiezaModelo> piezas = piso.PiezasDelModelo([medio]);
        Assert.Equal(["vt#1", "vt#2"], piezas.Select(x => x.Id));
        Assert.Equal((3000.0, 4500.0), (Math.Round(piezas[0].Inicio.Y), Math.Round(piezas[0].Fin.Y)));

        // Quitar solo la de arriba de la viga corta: queda de 0 a 4,50 m y de 6,00 a 9,00 m.
        List<(double Desde, double Hasta)> restos = PisoViguetas.Restos(vigueta, [(piezas[1].Desde, piezas[1].Hasta)]);
        Assert.Equal([(0.0, 4500.0), (6000.0, 9000.0)], restos.Select(t => (Math.Round(vigueta.En(t.Desde).Y), Math.Round(vigueta.En(t.Hasta).Y))));
        // Quitar las dos: quedan los tramos de los otros paneles; quitar todas las del piso: no queda nada.
        Assert.Equal(2, PisoViguetas.Restos(vigueta, piezas.Select(x => (x.Desde, x.Hasta))).Count);
        Assert.Empty(PisoViguetas.Restos(vigueta, piso.PiezasDelModelo(piso.Paneles).Select(x => (x.Desde, x.Hasta))));
    }

    /// <summary>Columna de tres paneles de 4,00 × 3,00 m entre ejes (vigas de 30 cm), como piso.</summary>
    private static PisoViguetas ColumnaDeTres()
    {
        List<ArmazonPiso> armazon =
        [
            new("izq", new(0, 0), new(0, 9000), 300, "V30X40", false),
            new("der", new(4000, 0), new(4000, 9000), 300, "V30X40", false),
        ];
        armazon.AddRange(new[] { 0, 3000, 6000, 9000 }.Select(y => new ArmazonPiso($"h{y}", new(0, y), new(4000, y), 300, "V30X40", false)));
        return new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);
    }

    [Fact]
    public void ElPerimetroConLasVigasDeAfueraEsUnoYLasDeAdentroSoloPartenLasViguetas()
    {
        PisoViguetas piso = ColumnaDeTres();

        Panel z = Assert.Single(piso.Perimetro(["izq", "der", "h0", "h9000"]));
        Assert.Equal("Z1", z.Id);
        Assert.Same(z, piso.Panel("Z1"));
        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;

        // Cada vigueta se parte en las dos vigas de adentro (tres piezas, una por P), pero se crea entera, de eje a eje.
        int n = p.Paneles[0].Reparto.Cantidad;
        Assert.Equal(3 * n, p.Viguetas.Count);
        Assert.Contains(p.Viguetas, v => v.Id == "Z1:1b");
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal(n, lineas.Count);
        Assert.All(lineas, l => Assert.Equal((0.0, 9000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
    }

    [Fact]
    public void QuitarUnaViguetaEnUnSoloPDejaLasQueSiguenHastaElEje()
    {
        PisoViguetas piso = ColumnaDeTres();
        Panel z = Assert.Single(piso.Perimetro(["izq", "der", "h0", "h9000"]));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string> { "Z1:1b" }, piso.Cruces)!;

        // La vigueta 1 pierde solo el tramo del P del medio: quedan dos, de 0 a 3,00 m y de 6,00 a 9,00 m, en el eje.
        double cota = p.Viguetas.First(v => v.Id == "Z1:1a").Cota;
        List<(double, double)> linea1 = p.Lineas(continuas: true).Where(l => Math.Abs(l.Desde.X - cota) < 1)
            .Select(l => (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))).ToList();
        Assert.Equal([(0.0, 3000.0), (6000.0, 9000.0)], linea1);
    }

    [Fact]
    public void ElPerimetroPuedeCerrarseConVoladizosYUnaViguetaDeBorde()
    {
        List<ArmazonPiso> armazon =
        [
            new("viga", new(0, 0), new(4000, 0), 300, "V30X40", false),
            new("vol1", new(0, 0), new(0, -1500), 250, "V25X40", false),
            new("vol2", new(4000, 0), new(4000, -1500), 250, "V25X40", false),
            new("borde", new(0, -1500), new(4000, -1500), 100, "VB_10X35", false),
        ];
        var piso = new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);

        // Sin elegirlo, el voladizo no es panel (la vigueta de borde no cierra); eligiéndolo, sí.
        Assert.Empty(piso.Paneles);
        Panel z = Assert.Single(piso.Perimetro(["viga", "vol1", "vol2", "borde"]));
        Assert.Equal(1500 - 150 - 50, Geometria.Cortes(z.Libre, false, 2000).Single() is var t ? t.Hasta - t.Desde : 0, 3);
        Assert.Empty(piso.Perimetro(["viga", "vol1", "vol2"]));
    }

    [Fact]
    public void EnElPerimetroLasNuevasSiguenLasDelModeloYSoloRellenanLosTramosQueFaltan()
    {
        // Columna de tres paneles con dos viguetas del modelo solo en el de arriba (x = 1,00 y 1,80 m), y el perímetro de
        // la columna entera: como en P1-P4-P6, las nuevas tienen que quedar en esas líneas y no encima de las que están.
        PisoViguetas piso = ColumnaDeTres();
        var conModelo = new PisoViguetas(piso.Piso with
        {
            Armazon = piso.Piso.Armazon.Concat(new[] { 1000, 1800 }.Select(x => new ArmazonPiso($"vt{x}", new(x, 6000), new(x, 9000), 100, "VT_10X35", false))).ToList(),
        }, false);
        Panel z = Assert.Single(conModelo.Perimetro(["izq", "der", "h0", "h9000"]));
        List<(Punto, Punto)> ocupadas = conModelo.PiezasDelModelo([z]).Select(x => (x.Inicio, x.Fin)).ToList();

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), conModelo.Cruces,
            conModelo.Guias(Direccion.Y), ocupadas)!;

        Assert.Equal(OrigenReparto.Existentes, Assert.Single(p.Paneles).Origen);
        Assert.Equal([1000.0, 1800.0], p.Viguetas.Select(v => Math.Round(v.Cota)).Distinct());
        // Arriba ya están: solo se crean de 0 a 6,00 m, en las dos líneas.
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal(2, lineas.Count);
        Assert.All(lineas, l => Assert.Equal((0.0, 6000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
    }

    /// <summary>Un piso con vigas de 30 cm (V30X40) por sus ejes, columnas y suelos opcionales (#954).</summary>
    private static PisoViguetas PisoDe(IEnumerable<(string Id, Punto Desde, Punto Hasta)> vigas, IReadOnlyList<ColumnaPiso>? columnas = null,
        IReadOnlyList<SueloPiso>? suelos = null) =>
        new(new PisoLeido("1", "N+3.70", 3700, vigas.Select(v => new ArmazonPiso(v.Id, v.Desde, v.Hasta, 300, "V30X40", false)).ToList(),
            columnas ?? [], suelos), false);

    /// <summary>Un rectángulo por sus esquinas, en sentido horario (las zonas se orientan solas).</summary>
    private static List<Punto> Rectangulo(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x0, y1), new(x1, y1), new(x1, y0)];

    /// <summary>Un cuadro de 4,00 × 3,00 m entre ejes, con las cuatro vigas o solo las que se pidan.</summary>
    private static List<(string, Punto, Punto)> Cuadro(params string[] lados)
    {
        List<(string Id, Punto, Punto)> todas =
        [
            ("abajo", new(0, 0), new(4000, 0)),
            ("der", new(4000, 0), new(4000, 3000)),
            ("arriba", new(4000, 3000), new(0, 3000)),
            ("izq", new(0, 3000), new(0, 0)),
        ];
        return lados.Length == 0 ? todas : todas.Where(v => lados.Contains(v.Id)).ToList();
    }

    [Fact]
    public void LosPuntosSonColumnasCrucesExtremosYVerticesDelSuelo()
    {
        var columna = new ColumnaPiso("c1", new(-150, -150), new(150, 150));
        var suelo = new SueloPiso("s1", [Rectangulo(0, 0, 2000, 3000)]);
        PisoViguetas piso = PisoDe(Cuadro(), [columna], [suelo]);
        var conCreada = new PisoViguetas(piso.Piso with
        {
            Armazon =
            [
                .. piso.Piso.Armazon,
                new ArmazonPiso("creada", new(1000, 1000), new(1000, 2000), 100, "VT_10X35", true),
                new ArmazonPiso("corta", new(3000, 0), new(3000, 1200), 200, "V20X30", false),
                new ArmazonPiso("vt", new(500, 3000), new(500, 4500), 100, "VT_10X35", false),
            ],
        }, false);

        List<PuntoPiso> puntos = conCreada.Puntos.ToList();

        // Cuatro esquinas (una es la columna), dos vértices del suelo que no son de vigas, la viga corta (cruce abajo y punta
        // adentro) y la vigueta (cruce y punta); nada repetido ni de lo creado. Cada uno con su tipo, para su color.
        Assert.Equal(10, puntos.Count);
        Assert.Equal("columna", puntos.Single(p => p.P == new Punto(0, 0)).Tipo);
        Assert.Equal(4, puntos.Count(p => p.Tipo == "cruce"));
        Assert.Equal("viga", puntos.Single(p => p.P == new Punto(3000, 1200)).Tipo);
        Assert.Equal(["vigueta", "vigueta"], puntos.Where(p => p.P.X == 500).OrderBy(p => p.P.Y).Select(p => p.Tipo));
        Assert.Equal([new Punto(2000, 0), new Punto(2000, 3000)], puntos.Where(p => p.Tipo == "suelo").Select(p => p.P).OrderBy(p => p.Y));
        Assert.DoesNotContain(puntos, p => p.P.X == 1000);
    }

    [Fact]
    public void UnaZonaSobreCuatroVigasEsComoUnPanel()
    {
        PisoViguetas piso = PisoDe(Cuadro());

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 4000, 3000), [])], 100));

        Assert.Equal("Z1", z.Id);
        Assert.Same(z, piso.Panel("Z1"));
        Assert.True(Geometria.Area(z.Eje) > 0);
        Assert.Equal(3700 * 2700, z.AreaLibre, 0);
        Assert.Empty(z.ViguetasDeBorde);
    }

    [Fact]
    public void UnLadoSinVigaLlevaUnaViguetaDeBordeHaciaAdentro()
    {
        PisoViguetas piso = PisoDe(Cuadro("abajo", "der", "izq"));

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 4000, 3000), [])], 100));

        // El lado de arriba no tiene viga: la vigueta de borde va con la cara al ras de y = 3,00 m (eje en 2,95 m) y el
        // libre llega a su otra cara (2,90 m).
        (Punto desde, Punto hasta) = Assert.Single(z.ViguetasDeBorde);
        Assert.Equal((2950.0, 2950.0), (desde.Y, hasta.Y));
        Assert.Equal(4000, Geometria.Distancia(desde, hasta), 3);
        Assert.Equal((150.0, 2900.0), (z.Libre.Min(q => q.Y), z.Libre.Max(q => q.Y)));
        Assert.Equal((150.0, 3850.0), (z.Libre.Min(q => q.X), z.Libre.Max(q => q.X)));
    }

    [Fact]
    public void UnLadoSobreVariasVigasSeguidasNoEsLibre()
    {
        // El lado de abajo de la zona va sobre dos vigas de 4,00 m (como en los modelos, una por vano): ninguna cubre más
        // de la mitad del lado, pero juntas lo cubren entero.
        PisoViguetas piso = PisoDe(
        [
            ("abajo1", new(0, 0), new(4000, 0)),
            ("abajo2", new(4000, 0), new(8000, 0)),
            ("der", new(8000, 0), new(8000, 3000)),
            ("arriba", new(8000, 3000), new(0, 3000)),
            ("izq", new(0, 3000), new(0, 0)),
        ]);

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 3000), [])], 100));

        Assert.Empty(z.ViguetasDeBorde);
        Assert.Equal(150, z.Libre.Min(q => q.Y), 3);
    }

    [Fact]
    public void UnLadoQuePasaPorElCuerpoDeLaVigaSinSuEjeNoLlevaViguetaDeBorde()
    {
        // Como en la prueba en Revit del 04-nov: los puntos elegidos no caen en el eje de la viga. El lado de abajo va 6 cm
        // por fuera del eje (dentro de su ancho de 30 cm) y el de la izquierda sale apenas inclinado (4 cm en 3 m).
        PisoViguetas piso = PisoDe(Cuadro());

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", [new(0, -60), new(4000, -60), new(4000, 3000), new(40, 3000)], [])], 100));

        Assert.Empty(z.ViguetasDeBorde);
        // El libre llega igual a la cara de adentro de la viga de abajo (y = 0,15 m), no a 0,15 m del lado.
        Assert.Equal(150, z.Libre.Min(q => q.Y), 0);
        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;
        Assert.Empty(p.DeBorde);
        // Y las viguetas llegan al eje de la viga (el apoyo), no al lado.
        Assert.All(p.Viguetas, v => Assert.Equal(0, v.EjeDesde, 0));
    }

    [Fact]
    public void UnElementoQueNoVaPorElLadoNoLoSostiene()
    {
        // Una viga paralela a 40 cm del lado (más que medio ancho + 3 cm) no lo sostiene: el lado es libre.
        PisoViguetas piso = PisoDe(Cuadro("der", "arriba", "izq").Append(("lejos", new Punto(0, -400), new Punto(4000, -400))));

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 4000, 3000), [])], 100));

        Assert.Single(z.ViguetasDeBorde);
    }

    [Fact]
    public void UnVacioParteLasViguetasYLlevaViguetasDeBordeHaciaLaLosa()
    {
        PisoViguetas piso = PisoDe(
        [
            ("abajo", new(0, 0), new(6000, 0)),
            ("der", new(6000, 0), new(6000, 4000)),
            ("arriba", new(6000, 4000), new(0, 4000)),
            ("izq", new(0, 4000), new(0, 0)),
        ]);

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 6000, 4000), [Rectangulo(2500, 1500, 3500, 2500)])], 100));

        // Los cuatro lados del vacío son libres: sus viguetas de borde van por fuera del vacío (eje a 5 cm) y el libre del
        // vacío crece un ancho de vigueta.
        Assert.Equal(4, z.ViguetasDeBorde.Count);
        Assert.Contains(z.ViguetasDeBorde, b => b.Desde.Y == 1450 && b.Hasta.Y == 1450);
        Assert.Contains(z.ViguetasDeBorde, b => b.Desde.X == 3550 && b.Hasta.X == 3550);
        IReadOnlyList<Punto> hueco = Assert.Single(z.HuecosLibre!);
        Assert.Equal((2400.0, 3600.0, 1400.0, 2600.0), (hueco.Min(q => q.X), hueco.Max(q => q.X), hueco.Min(q => q.Y), hueco.Max(q => q.Y)));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;

        // Las que pasan por el vacío paran en él: dos piezas, del borde de la losa a la cara de su vigueta de borde.
        List<Vigueta> partidas = p.Viguetas.Where(v => v.Cota > 2400 && v.Cota < 3600).ToList();
        Assert.NotEmpty(partidas);
        foreach (IGrouping<double, Vigueta> fila in partidas.GroupBy(v => v.Cota))
        {
            Assert.Equal([(150.0, 1400.0), (2600.0, 3850.0)], fila.OrderBy(v => v.Desde).Select(v => (Math.Round(v.Desde), Math.Round(v.Hasta))));
            Assert.All(fila, v => Assert.Matches(@"^Z1:\d+\.[12]$", v.Id));
            // Sus líneas de Revit llegan al eje de las viguetas de borde del vacío (a 5 cm por fuera de él).
            Assert.Equal([(0.0, 1450.0), (2550.0, 4000.0)], fila.OrderBy(v => v.Desde).Select(v => (Math.Round(v.EjeDesde), Math.Round(v.EjeHasta))));
        }
        Assert.All(p.Viguetas.Where(v => v.Cota < 2400 || v.Cota > 3600), v => Assert.Equal((150.0, 3850.0), (Math.Round(v.Desde), Math.Round(v.Hasta))));
        Assert.Equal(["Z1:B1", "Z1:B2", "Z1:B3", "Z1:B4"], p.DeBorde.Select(b => b.Id));
    }

    [Fact]
    public void DosZonasSeparadasSeProponenJuntas()
    {
        PisoViguetas piso = PisoDe(Grilla().Select(b => (b.Id, b.Desde, b.Hasta)));

        IReadOnlyList<Panel> zonas = piso.Zonas(
        [
            new ZonaPuntos("Z1", Rectangulo(0, 0, 4000, 3000), []),
            new ZonaPuntos("Z2", Rectangulo(4000, 0, 8000, 3000), []),
        ], 100);

        Assert.Equal(["Z1", "Z2"], zonas.Select(z => z.Id));
        Assert.NotNull(piso.Panel("Z1"));
        PropuestaViguetas p = PlanificadorViguetas.Planificar(piso.PanelesDe(["Z1", "Z2"]), Direccion.X, 800, 100, new HashSet<string>(), piso.Cruces)!;
        Assert.Equal(["Z1", "Z2"], p.Paneles.Select(r => r.Panel));
        Assert.Contains(p.Viguetas, v => v.Panel == "Z2");
        // Volver a mandarlas sin la Z1 la quita.
        Assert.Single(piso.Zonas([new ZonaPuntos("Z2", Rectangulo(4000, 0, 8000, 3000), [])], 100));
        Assert.Null(piso.Panel("Z1"));
        // Un perímetro «Por vigas» también se llama Z1: armarlo borra las zonas, y armar zonas lo borra a él.
        Assert.Single(piso.Perimetro(["abajo", "arriba", "A", "B"]));
        Assert.Null(piso.Panel("Z2"));
        Panel zona = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(4000, 0, 8000, 3000), [])], 100));
        Assert.Same(zona, piso.Panel("Z1"));
    }

    [Fact]
    public void UnaVigaParalelaDentroDeLaZonaParteElRepartoYNingunaViguetaQuedaEncima()
    {
        // Como en la prueba en Revit del 04-nov: una zona sobre dos paneles lado a lado (la grilla de 8,00 × 3,00 m), con
        // viguetas en ↕ Y, paralelas a la viga del medio (x = 4,00 m, 30 cm de ancho).
        PisoViguetas piso = PisoDe(Grilla().Select(b => (b.Id, b.Desde, b.Hasta)));
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 3000), [])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        // Ninguna toca el cuerpo de la viga del medio (de 3,85 a 4,15 m, más medio ancho de vigueta).
        List<double> cotas = p.Viguetas.Select(v => Math.Round(v.Cota, 3)).Distinct().ToList();
        Assert.All(cotas, c => Assert.False(c > 3850 - 50 && c < 4150 + 50, $"Vigueta en x = {c} encima de la viga del medio"));
        // Cada lado se reparte con la fórmula sobre su libre de 3,70 m, como si fueran dos paneles.
        Reparto r = RepartoViguetas.Calcular(3700, 800, 100)!;
        List<double> izquierda = cotas.Where(c => c < 4000).ToList(), derecha = cotas.Where(c => c > 4000).ToList();
        Assert.Equal(r.Cantidad, izquierda.Count);
        Assert.Equal(r.Cantidad, derecha.Count);
        Assert.Equal(150 + r.LibreExtremos + 50, izquierda[0], 3);
        Assert.Equal(4150 + r.LibreExtremos + 50, derecha[0], 3);
        // Sin las vigas (como antes), la fórmula reparte de punta a punta y una cae sobre la del medio.
        PropuestaViguetas antes = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;
        Assert.Contains(antes.Viguetas, v => v.Cota > 3800 && v.Cota < 4200);
    }

    [Fact]
    public void LasViguetasDeBordeSeCreanConLasDemasYSePuedenQuitar()
    {
        PisoViguetas piso = PisoDe(Cuadro("abajo", "der", "izq"));
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 4000, 3000), [])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;
        PropuestaViguetas sinBorde = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string> { "Z1:B1" }, piso.Cruces)!;

        ViguetaBorde borde = Assert.Single(p.DeBorde);
        Assert.Equal(("Z1:B1", "Z1", false), (borde.Id, borde.Panel, borde.Borrada));
        Assert.Contains((borde.Desde, borde.Hasta), p.Lineas(continuas: true));
        Assert.Equal(p.Lineas(continuas: true).Count - 1, sinBorde.Lineas(continuas: true).Count);
        Assert.True(Assert.Single(sinBorde.DeBorde).Borrada);
        // Las viguetas del reparto llegan a la cara de la de borde y su línea de Revit, a su eje (decisión 4); del otro lado,
        // al eje de la viga.
        Assert.All(p.Viguetas, v => Assert.Equal((2900.0, 0.0, 2950.0), (Math.Round(v.Hasta, 3), Math.Round(v.EjeDesde, 3), Math.Round(v.EjeHasta, 3))));
        Assert.All(p.Lineas(continuas: true).Where(l => l != (borde.Desde, borde.Hasta)), l => Assert.Equal(2950, Math.Max(l.Desde.Y, l.Hasta.Y), 3));
    }

    [Fact]
    public void ElProtocoloLlevaSuelosPuntosZonasYBordes()
    {
        // La página manda todas sus zonas juntas; un punto roto se salta.
        ArmarZonas pedido = Assert.IsType<ArmarZonas>(ProtocoloViguetas.Leer(
            """{"tipo":"zonas","ancho":100,"zonas":[{"id":"Z1","contorno":[[0,0],[0,3000],[4000,3000],[4000,0],[1]],"vacios":[[[1500,1000],[1500,2000],[2500,2000],[2500,1000]]]}]}"""));
        Assert.Equal(100, pedido.Ancho);
        ZonaPuntos zona = Assert.Single(pedido.Zonas);
        Assert.Equal(4, zona.Contorno.Count);
        Assert.Equal(new Punto(1500, 1000), Assert.Single(zona.Vacios)[0]);
        Assert.Empty(Assert.IsType<ArmarZonas>(ProtocoloViguetas.Leer("""{"tipo":"zonas"}""")).Zonas);

        PisoViguetas piso = PisoDe(Cuadro("abajo", "der", "izq"), suelos: [new SueloPiso("s1", [Rectangulo(0, 0, 4000, 3000)])]);
        using var json = System.Text.Json.JsonDocument.Parse(ProtocoloViguetas.Piso(piso));
        Assert.Equal("s1", json.RootElement.GetProperty("suelos")[0].GetProperty("id").GetString());
        Assert.Contains(json.RootElement.GetProperty("puntos").EnumerateArray(), p => p.GetProperty("tipo").GetString() == "cruce");

        IReadOnlyList<Panel> zonas = piso.Zonas(pedido.Zonas, pedido.Ancho);
        using var respuesta = System.Text.Json.JsonDocument.Parse(ProtocoloViguetas.Zonas(zonas.Select(z => (z, 0))));
        System.Text.Json.JsonElement z1 = Assert.Single(respuesta.RootElement.GetProperty("paneles").EnumerateArray());
        Assert.Equal("zonas", respuesta.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(1, z1.GetProperty("huecosLibre").GetArrayLength());
        // El lado de arriba sin viga y los cuatro del vacío.
        Assert.Equal(5, z1.GetProperty("bordes").GetArrayLength());

        PropuestaViguetas p = PlanificadorViguetas.Planificar(zonas, Direccion.Y, 800, 100, new HashSet<string> { "Z1:B1" }, piso.Cruces)!;
        using var propuesta = System.Text.Json.JsonDocument.Parse(ProtocoloViguetas.Propuesta(p, ["Z1"], true));
        System.Text.Json.JsonElement bordes = propuesta.RootElement.GetProperty("propuesta").GetProperty("bordes");
        Assert.Equal(5, bordes.GetArrayLength());
        Assert.True(bordes[0].GetProperty("borrada").GetBoolean());
        Assert.Equal("Z1:B1", bordes[0].GetProperty("id").GetString());
    }

    [Fact]
    public void ElMensajeDeCrearTraeElPedidoYElTipo()
    {
        MensajeViguetas m = ProtocoloViguetas.Leer(
            """{"tipo":"crear","paneles":["P2","P5"],"direccion":"Y","ejeAEje":800,"ancho":100,"borradas":["P2:1","P5:3a"],"continuas":false,"tipoVigueta":"123"}""");

        CrearViguetas c = Assert.IsType<CrearViguetas>(m);
        Assert.Equal("123", c.Tipo);
        Assert.Equal(new[] { "P2", "P5" }, c.Propuesta.Paneles);
        Assert.Equal(new[] { "P2:1", "P5:3a" }, c.Propuesta.Borradas);
        Assert.Equal(Direccion.Y, c.Propuesta.Direccion);
        Assert.False(c.Propuesta.Continuas);
        Assert.True(c.Propuesta.Seguir);
        Assert.Empty(c.Propuesta.DelModelo);
        Assert.IsType<DesconocidoViguetas>(ProtocoloViguetas.Leer("{roto"));
    }
}
