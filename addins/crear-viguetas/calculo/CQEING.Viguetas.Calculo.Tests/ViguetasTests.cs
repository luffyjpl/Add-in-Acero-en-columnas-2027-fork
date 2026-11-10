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
    // Decisión 15 (Santiago, 05-nov): «si hay 90 se pone a 45; si es menor a 80 no se pone». Hasta 0,70 de libre (eje a
    // eje − ancho) no va ninguna; 0,75 y 0,90, una al centro.
    [InlineData(600, 0, 600)]
    [InlineData(700, 0, 700)]
    [InlineData(750, 1, 325)]
    [InlineData(900, 1, 400)]
    public void SiElLibreCabeSinViguetaNoVaNinguna(double libre, int cantidad, double extremos)
    {
        Reparto r = RepartoViguetas.Calcular(libre, 800, 100)!;

        Assert.Equal(cantidad, r.Cantidad);
        Assert.Equal(extremos, r.LibreExtremos, 6);
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
        // Los tipos van por vigueta creada (#954, decisión 19): cada una cruza la viga del medio de punta a punta, así que
        // son tres viguetas iguales de 7,70 m libres (no seis piezas de 3,70 m), un solo tipo.
        Assert.Equal(new TipoVigueta(1, 7700, 3), Assert.Single(p.Tipos));
        Assert.All(p.Viguetas, v => Assert.Equal(1, v.Tipo));
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
        // Las dos de arriba, y donde no llegan, la fórmula (#954, decisión 26): a la izquierda 0,80 libres → una en 0,55;
        // entre ellas 1,10 → una en 1,60; a la derecha 1,00 → una en 2,75. Antes quedaban esos huecos sin viguetas.
        Assert.Equal([550.0, 1000.0, 1600.0, 2200.0, 2750.0], p.Viguetas.Select(v => v.Cota));
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
        // En esas dos líneas y, donde no llegan, la fórmula (#954, decisión 26): 0,55 a la izquierda; 2,45 y 3,25 a la derecha.
        Assert.Equal([550.0, 1000.0, 1800.0, 2450.0, 3250.0], p.Viguetas.Select(v => Math.Round(v.Cota)).Distinct().Order());
        // En las dos líneas del modelo, arriba ya están: solo se crean de 0 a 6,00 m. Las otras, de punta a punta.
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.All(lineas.Where(l => Math.Round(l.Desde.X) is 1000 or 1800), l => Assert.Equal((0.0, 6000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
        Assert.All(lineas.Where(l => Math.Round(l.Desde.X) is not (1000 or 1800)), l => Assert.Equal((0.0, 9000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
        Assert.Equal(5, lineas.Count);
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
        // adentro), la vigueta (cruce y punta) y las dos puntas de la creada (decisión 24); nada repetido. Cada uno con su
        // tipo, para su color.
        Assert.Equal(12, puntos.Count);
        Assert.Equal("columna", puntos.Single(p => p.P == new Punto(0, 0)).Tipo);
        Assert.Equal(4, puntos.Count(p => p.Tipo == "cruce"));
        Assert.Equal("viga", puntos.Single(p => p.P == new Punto(3000, 1200)).Tipo);
        Assert.Equal(["vigueta", "vigueta"], puntos.Where(p => p.P.X == 500).OrderBy(p => p.P.Y).Select(p => p.Tipo));
        Assert.Equal([new Punto(2000, 0), new Punto(2000, 3000)], puntos.Where(p => p.Tipo == "suelo").Select(p => p.P).OrderBy(p => p.Y));
        // La creada por el add-in da sus dos puntas (decisión 24), como vigueta.
        Assert.Equal([new Punto(1000, 1000), new Punto(1000, 2000)], puntos.Where(p => p.P.X == 1000).Select(p => p.P).OrderBy(p => p.Y));
        Assert.All(puntos.Where(p => p.P.X == 1000), p => Assert.Equal("vigueta", p.Tipo));
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
            // Arriba y abajo del vacío son regiones distintas (decisión 18): cada pieza con su propio id.
            Assert.All(fila, v => Assert.Matches(@"^Z1:\d+$", v.Id));
            Assert.Equal(2, fila.Select(v => v.Id).Distinct().Count());
            // Sus líneas de Revit llegan al eje de las viguetas de borde del vacío (a 5 cm por fuera de él).
            Assert.Equal([(0.0, 1450.0), (2550.0, 4000.0)], fila.OrderBy(v => v.Desde).Select(v => (Math.Round(v.EjeDesde), Math.Round(v.EjeHasta))));
        }
        Assert.All(p.Viguetas.Where(v => v.Cota < 2400 || v.Cota > 3600), v => Assert.Equal((150.0, 3850.0), (Math.Round(v.Desde), Math.Round(v.Hasta))));
        Assert.Equal(["Z1:B1", "Z1:B2", "Z1:B3", "Z1:B4"], p.DeBorde.Select(b => b.Id));
    }

    [Fact]
    public void LaViguetaDeBordeDelVacioEnElSentidoDeLasViguetasLlegaAlPerimetroYParteElReparto()
    {
        // Como en el piso 2 de Santiago (05-nov): un vacío pegado a la viga de la derecha y apoyado en la viga del medio.
        PisoViguetas piso = PisoDe(
        [
            ("abajo", new(0, 0), new(6000, 0)),
            ("der", new(6000, 0), new(6000, 8000)),
            ("arriba", new(6000, 8000), new(0, 8000)),
            ("izq", new(0, 8000), new(0, 0)),
            ("medio", new(0, 4000), new(6000, 4000)),
        ]);
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 6000, 8000), [Rectangulo(4800, 4000, 6000, 5000)])], 100));
        // Solo el lado de arriba y el de la izquierda del vacío son libres.
        Assert.Equal(2, z.ViguetasDeBorde.Count);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 1000, 100, new HashSet<string>(), piso.Cruces,
            divisiones: piso.Vigas)!;

        // Decisión 11, corregida por la 22: la del lado izquierdo del vacío sigue por su línea hasta las vigas que la
        // cruzan. El vacío va sobre la viga del medio, así que va de ella (y = 4,00 m) a la de arriba, no hasta abajo.
        ViguetaBorde izquierda = Assert.Single(p.DeBorde, b => b.DeVacio && b.Desde.X == b.Hasta.X);
        Assert.Equal(4750, izquierda.Desde.X);
        Assert.Equal((4000.0, 8000.0), (Math.Min(izquierda.Desde.Y, izquierda.Hasta.Y), Math.Max(izquierda.Desde.Y, izquierda.Hasta.Y)));
        // Decisión 25: el panel de abajo de la viga del medio sigue las líneas del de arriba (manda el de arriba, más angosto
        // por el vacío), sigue la línea de la vigueta de borde del vacío (4,75 m) y de ahí a la viga, la fórmula centrada:
        // una en 5,325 m.
        List<double> deArriba = p.Viguetas.Where(v => v.Desde > 4000 && v.Cota < 4700).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        Assert.Equal([.. deArriba, 4750.0, 5325.0], p.Viguetas.Where(v => v.Hasta < 4000).Select(v => Math.Round(v.Cota)).Distinct().Order());
        // La de arriba del vacío sigue a lo largo de él.
        ViguetaBorde arriba = Assert.Single(p.DeBorde, b => b.Desde.Y == b.Hasta.Y);
        Assert.Equal(5050, arriba.Desde.Y);

        // Arriba, ninguna vigueta queda encima de ella ni corrida al lado: el reparto se parte en su línea.
        Assert.DoesNotContain(p.Viguetas, v => v.Desde > 4000 && v.Cota + 50 > 4700 && v.Cota - 50 < 4800);
        List<double> cotas = p.Viguetas.Where(v => v.Desde > 4000).Select(v => v.Cota).Distinct().Order().ToList();
        List<double> izquierdaDeLaLinea = cotas.Where(c => c < 4750).ToList(), derecha = cotas.Where(c => c > 4750).ToList();
        Assert.NotEmpty(izquierdaDeLaLinea);
        Assert.NotEmpty(derecha);
        // Cada tramo se reparte por su cuenta con los dos extremos iguales: de la viga de la izquierda a la línea, y de la
        // línea a la viga de la derecha (vigas de 30 cm: caras a 150 y 5850).
        Assert.Equal(Math.Round(izquierdaDeLaLinea[0] - 50 - 150), Math.Round(4700 - (izquierdaDeLaLinea[^1] + 50)));
        Assert.Equal(Math.Round(derecha[0] - 50 - 4800), Math.Round(5850 - (derecha[^1] + 50)));
        // Las de la derecha paran en el vacío; las de la izquierda van enteras.
        Assert.All(p.Viguetas.Where(v => v.Cota > 4750 && v.EjeDesde > 4500), v => Assert.True(v.Desde >= 5100 - 1));

        // Eliminada, deja de partir el reparto.
        PropuestaViguetas sin = PlanificadorViguetas.Planificar([z], Direccion.Y, 1000, 100, new HashSet<string> { izquierda.Id }, piso.Cruces,
            divisiones: piso.Vigas)!;
        Assert.True(Assert.Single(sin.DeBorde, b => b.Id == izquierda.Id).Borrada);
        Assert.NotEqual(cotas, sin.Viguetas.Where(v => v.Desde > 4000).Select(v => v.Cota).Distinct().Order().ToList());
    }

    [Fact]
    public void UnPuntoDentroDelCuerpoDeLaVigaVaASuEjeYNoLlevaViguetaDeBordeEncima()
    {
        // Como en la captura del 05-nov: un vértice del suelo a 14 cm del eje de la viga de arriba (de 30 cm).
        PisoViguetas piso = PisoDe(
        [
            ("abajo", new(0, 0), new(3000, 0)),
            ("der", new(3000, 0), new(3000, 4000)),
            ("arriba", new(3000, 4000), new(0, 4000)),
            ("izq", new(0, 4000), new(0, 0)),
        ]);
        List<Punto> contorno = [new(0, 0), new(0, 4000), new(1500, 4140), new(3000, 4000), new(3000, 0)];

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", contorno, [])], 100));

        Assert.Empty(z.ViguetasDeBorde);
        Assert.All(z.Eje, q => Assert.True(q.Y is 0 or 4000));
        Assert.Equal(3850, z.Libre.Max(q => q.Y));
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

    /// <summary>
    /// Como el N+3.70 de la reunión del 05-nov: dos luces de 4,00 m (ejes x = 0, 4,00 y 8,00 m) y 6,00 m de alto, con una
    /// viga a media altura (y = 3,00 m). Las vigas horizontales van una por vano y terminan en la cara de la viga del medio
    /// (x = 3,85 y 4,15 m), como en los modelos.
    /// </summary>
    private static PisoViguetas DosLucesPorVano() => PisoDe(
    [
        ("A", new(0, 0), new(0, 6000)),
        ("B", new(4000, 0), new(4000, 6000)),
        ("C", new(8000, 0), new(8000, 6000)),
        ("h0a", new(0, 0), new(3850, 0)), ("h0b", new(4150, 0), new(8000, 0)),
        ("h3a", new(0, 3000), new(3850, 3000)), ("h3b", new(4150, 3000), new(8000, 3000)),
        ("h6a", new(0, 6000), new(3850, 6000)), ("h6b", new(4150, 6000), new(8000, 6000)),
    ]);

    [Fact]
    public void LasVigasDeCadaVanoQueLleganALaDelMedioNoAmontonanViguetasNiLasCortan()
    {
        // Reunión del 05-nov: «separando cada 33 si habíamos establecido 80», «crea cuatro barras donde debe crear dos» y
        // «esa y la de arriba es una sola» (decisiones 16 y 19).
        PisoViguetas piso = DosLucesPorVano();
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        // Cada luz, con la fórmula sobre su libre de 3,70 m: cuatro a 0,80, con 0,60 en los extremos. Nada más.
        Reparto r = RepartoViguetas.Calcular(3700, 800, 100)!;
        Assert.Equal((4, 600.0), (r.Cantidad, r.LibreExtremos));
        List<double> cotas = p.Viguetas.Select(v => Math.Round(v.Cota, 3)).Distinct().Order().ToList();
        Assert.Equal([800.0, 1600.0, 2400.0, 3200.0, 4800.0, 5600.0, 6400.0, 7200.0], cotas);
        Assert.All(p.Paneles, x => Assert.Equal(800, x.Reparto.EjeAEje));
        // La viga de y = 3,00 m termina en la del medio, no suelta: no pone topes. Cada vigueta va de punta a punta, una
        // sola, y todas son del mismo tipo (mismo largo, mismos apoyos).
        Assert.All(p.Viguetas, v => Assert.False(v.TopeDesde || v.TopeHasta));
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal(8, lineas.Count);
        Assert.All(lineas, l => Assert.Equal((0.0, 6000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
        Assert.Equal(new TipoVigueta(1, 5700, 8), Assert.Single(p.Tipos));
    }

    [Fact]
    public void UnaVigaCortaSueltaSiguePoniendoSuViguetaEnLaPunta()
    {
        // La viga corta de verdad (decisiones 30 y 31 de #947) sigue igual: su punta queda suelta dentro del panel.
        PisoViguetas piso = DosLucesPorVano();
        var conCorta = new PisoViguetas(piso.Piso with
        {
            Armazon = [.. piso.Piso.Armazon, new ArmazonPiso("corta", new(0, 1500), new(1675, 1500), 200, "V20X30", false)],
        }, false);
        Panel z = Assert.Single(conCorta.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), conCorta.Cruces, divisiones: conCorta.Vigas)!;

        // Una vigueta por dentro de la viga, con la cara al ras de su punta (eje en 1,625 m), en un reparto parejo de
        // cuatro a 0,75 con extremos de 0,675 (decisiones 17 y 23): pareja, centrada y no más apretada.
        List<double> izquierda = p.Viguetas.Where(v => v.Cota < 4000 && v.Desde < 3000).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        Assert.Equal([875.0, 1625.0, 2375.0, 3125.0], izquierda);
        Assert.Contains(p.Viguetas, v => v.TopeDesde || v.TopeHasta);
    }

    [Fact]
    public void AbajoDeUnVacioQueTomaTodaLaLuzSeReparteElAnchoCompletoDeAbajo()
    {
        // Reunión del 05-nov: «faltan viguetas abajo… los nervios de arriba no son los mismos de abajo» (decisión 18). Un
        // vacío en L en la luz izquierda, de viga a viga: abajo toma todo el ancho (de y = 3,50 a 4,00 m) y arriba solo
        // de x = 2,50 a 4,00 m (de y = 4,00 a 4,50 m). Su lado vertical libre (x = 2,50 m) lleva una vigueta de borde que
        // sube hasta el perímetro (decisión 11) y parte el reparto solo arriba.
        PisoViguetas piso = DosLucesPorVano();
        List<Punto> vacioL = [new(0, 3500), new(4000, 3500), new(4000, 4500), new(2500, 4500), new(2500, 4000), new(0, 4000)];
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [vacioL])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        List<double> Cotas(double w) => p.Viguetas.Where(v => v.Cota < 4000 && v.Desde <= w && w <= v.Hasta).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        // Abajo del vacío: la fórmula sobre todo el ancho de la luz (3,70 m), como si estuviera sola.
        Assert.Equal([800.0, 1600.0, 2400.0, 3200.0], Cotas(1000));
        // Arriba a la izquierda, entre la viga A y la vigueta de borde del vacío (de 0,15 a 2,40 m): dos, centradas.
        Assert.Equal([875.0, 1675.0], Cotas(5000).Where(c => c < 2400));
        // Arriba del brazo del vacío, de 2,50 a 3,85 m: una al centro.
        Assert.Equal([3175.0], Cotas(5000).Where(c => c > 2500));
        // En Revit, las de abajo van del eje de la viga de abajo al de la vigueta de borde del vacío (y = 3,45 m), una sola
        // a través de la viga de y = 3,00 m; las de arriba a la izquierda, del vacío (4,05 m) a la viga de arriba.
        List<(Punto Desde, Punto Hasta)> izquierda = p.Lineas(continuas: true).Where(l => l.Desde.X < 4000 && Math.Abs(l.Desde.X - l.Hasta.X) < 1).ToList();
        Assert.Equal(4, izquierda.Count(l => Math.Round(l.Desde.Y) == 0 && Math.Round(l.Hasta.Y) == 3450));
        Assert.Equal(2, izquierda.Count(l => Math.Round(l.Desde.Y) == 4050 && Math.Round(l.Hasta.Y) == 6000));
        // Cada región trae su reparto, en su lugar, para las cotas de la página.
        Assert.Contains(p.Paneles, r => r.Region is { } g && Math.Round(g.Desde) == 150 && Math.Round(g.Hasta) == 3850 && Math.Round(g.WHasta) == 3400);
        // La luz derecha no cambia: cuatro de abajo arriba.
        Assert.Equal(4, p.Viguetas.Where(v => v.Cota > 4000).Select(v => Math.Round(v.Cota)).Distinct().Count());
        // Abajo y arriba del vacío son tipos distintos: no tienen el mismo largo ni los mismos apoyos.
        int TipoEn(double cota, double w) => p.Viguetas.First(v => Math.Round(v.Cota) == cota && v.Desde <= w && w <= v.Hasta).Tipo;
        Assert.NotEqual(TipoEn(800, 1000), TipoEn(875, 5000));
    }

    [Fact]
    public void UnVacioChicoNoCruzaLasVigasYElPanelDeAbajoSigueSusLineas()
    {
        // Prueba en Revit del 05-nov (vacío 2): sus viguetas de borde no cruzan las vigas (decisión 22) y el panel de abajo
        // sigue las líneas del de arriba, que manda (decisión 25). Un vacío de 1,00 × 1,00 m en el panel de arriba a la
        // derecha, con sus cuatro lados libres.
        PisoViguetas piso = DosLucesPorVano();
        Panel sinVacio = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [])], 100));
        PropuestaViguetas antes = PlanificadorViguetas.Planificar([sinVacio], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [Rectangulo(5500, 4000, 6500, 5000)])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        // Sus viguetas de borde verticales suben hasta la viga de arriba (y = 6,00 m) y bajan solo hasta la viga del medio
        // (y = 3,00 m), no hasta el borde de abajo.
        List<ViguetaBorde> verticales = p.DeBorde.Where(b => b.DeVacio && Math.Abs(b.Desde.X - b.Hasta.X) < 1).ToList();
        Assert.Equal(2, verticales.Count);
        Assert.All(verticales, b => Assert.Equal((3000.0, 6000.0), (Math.Round(Math.Min(b.Desde.Y, b.Hasta.Y)), Math.Round(Math.Max(b.Desde.Y, b.Hasta.Y)))));
        // Abajo de esa viga, el panel de la derecha sigue las líneas de arriba que llegan a la viga: a los costados del
        // vacío, 4,775 y 7,225 m; las de sus viguetas de borde, 5,45 y 6,55 m; y la de abajo del vacío, 6,00 m.
        List<double> abajo = p.Viguetas.Where(v => v.Cota > 4000 && v.Desde < 2000).Select(v => v.Cota).Distinct().Order().ToList();
        Assert.Equal([4775.0, 5450.0, 6000.0, 6550.0, 7225.0], abajo);
        // Las de los costados son una sola vigueta de abajo arriba, a través de la viga del medio.
        Assert.Contains(p.Lineas(continuas: true), l => Math.Abs(l.Desde.X - 4775) < 1 && Math.Round(l.Desde.Y) == 0 && Math.Round(l.Hasta.Y) == 6000);
        // La luz de la izquierda no cambia.
        List<double> Izquierda(PropuestaViguetas x) => x.Viguetas.Where(v => v.Cota < 4000).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        Assert.Equal(Izquierda(antes), Izquierda(p));
        // Ninguna separación más chica que la mitad del eje a eje.
        Assert.All(p.Paneles.Where(r => r.Reparto.Cantidad > 1), r => Assert.True(r.Reparto.EjeAEje >= 400, $"eje a eje {r.Reparto.EjeAEje}"));
    }

    [Fact]
    public void SiElLadoDelVacioVaSobreUnaVigaSuViguetaDeBordeNoPasaDeEsaViga()
    {
        // Como el vacío 1 de la prueba del 05-nov: su lado de abajo va sobre la viga de y = 3,00 m. Sus viguetas de borde
        // verticales suben hasta la viga de arriba y no bajan al panel de abajo (decisión 22). El de abajo sigue las líneas
        // de los costados del vacío y las de sus viguetas de borde (decisión 25): 4,525, 4,95, 6,05, 6,575 y 7,375 m, y
        // entre 4,95 y 6,05, la fórmula centrada (5,50).
        PisoViguetas piso = DosLucesPorVano();
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [Rectangulo(5000, 3000, 6000, 4500)])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        List<ViguetaBorde> verticales = p.DeBorde.Where(b => b.DeVacio && Math.Abs(b.Desde.X - b.Hasta.X) < 1).ToList();
        Assert.Equal(2, verticales.Count);
        Assert.All(verticales, b => Assert.Equal((3000.0, 6000.0), (Math.Round(Math.Min(b.Desde.Y, b.Hasta.Y)), Math.Round(Math.Max(b.Desde.Y, b.Hasta.Y)))));
        Assert.Equal([4525.0, 4950.0, 5500.0, 6050.0, 6575.0, 7375.0], p.Viguetas.Where(v => v.Cota > 4000 && v.Desde < 2000).Select(v => Math.Round(v.Cota)).Distinct().Order());
    }

    [Fact]
    public void ElPanelDeArribaSigueLasLineasDelDeAbajoQueTieneElVacio()
    {
        // Captura 1 del 05-nov: el vacío está en el panel de abajo, contra la viga del medio y la de la derecha. La franja
        // de al lado del vacío fija sus líneas («la T6 pone el orden»): siguen hacia arriba a través de la viga, una sola
        // vigueta y un solo tipo, y arriba del vacío sigue el mismo ritmo («también debe salir la que abajo no se genera»).
        PisoViguetas piso = DosLucesPorVano();
        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [Rectangulo(5600, 600, 8000, 3000)])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        // Al lado del vacío (de 4,15 a 5,50 m): una al centro, en 4,825 m. Arriba: esa misma, la de la línea de la vigueta de
        // borde del vacío (5,55 m, «hay una que no alinea») y, de ahí a la viga, la fórmula centrada: 6,325 y 7,125 m.
        Assert.Equal([4825.0], p.Viguetas.Where(v => v.Cota > 4000 && v.Desde < 2000 && v.Hasta > 2000).Select(v => Math.Round(v.Cota)).Distinct());
        Assert.Equal([4825.0, 5550.0, 6325.0, 7125.0], p.Viguetas.Where(v => v.Cota > 4000 && v.Desde > 3000).Select(v => Math.Round(v.Cota)).Distinct().Order());
        (Punto desde, Punto hasta) = Assert.Single(p.Lineas(continuas: true), l => Math.Abs(l.Desde.X - 4825) < 1);
        Assert.Equal((0.0, 6000.0), (Math.Round(desde.Y), Math.Round(hasta.Y)));
        Assert.Single(p.Viguetas.Where(v => Math.Round(v.Cota) == 4825).Select(v => v.Tipo).Distinct());
        // La vigueta de borde del vacío y la que la sigue arriba se crean como una sola, de 0 a 6,00 m.
        (Punto bd, Punto bh) = Assert.Single(p.Lineas(continuas: true), l => Math.Abs(l.Desde.X - 5550) < 1 && Math.Abs(l.Hasta.X - 5550) < 1);
        Assert.Equal((0.0, 6000.0), (Math.Round(Math.Min(bd.Y, bh.Y)), Math.Round(Math.Max(bd.Y, bh.Y))));
        // Ninguna queda pegada a la viga de la derecha (cara en 7,85 m).
        Assert.True(7850 - (p.Viguetas.Max(v => v.Cota) + 50) >= 300);
    }

    [Fact]
    public void ElPanelDeAbajoSigueLasLineasDelDeArribaQueTieneLaVigaCorta()
    {
        // Captura 2 del 05-nov: arriba hay una viga corta con su punta suelta, que fija las líneas (decisión 31); el panel
        // de abajo no tiene nada particular y las sigue a través de la viga del medio («la que manda es la de arriba»).
        PisoViguetas piso = DosLucesPorVano();
        var conCorta = new PisoViguetas(piso.Piso with
        {
            Armazon = [.. piso.Piso.Armazon, new ArmazonPiso("corta", new(0, 4500), new(1675, 4500), 200, "V20X30", false)],
        }, false);
        Panel z = Assert.Single(conCorta.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 8000, 6000), [])], 100));

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), conCorta.Cruces, divisiones: conCorta.Vigas)!;

        List<double> arriba = p.Viguetas.Where(v => v.Cota < 4000 && v.Desde > 3000).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        List<double> abajo = p.Viguetas.Where(v => v.Cota < 4000 && v.Desde < 3000).Select(v => Math.Round(v.Cota)).Distinct().Order().ToList();
        Assert.Equal([875.0, 1625.0, 2375.0, 3125.0], arriba);
        Assert.Equal(arriba, abajo);
    }

    [Fact]
    public void EnUnaFranjaAngostaLaPuntaDeUnaVigaNoApretaNiDescuadraElReparto()
    {
        // Prueba en Revit del 05-nov: dos viguetas a 0,30 en una franja de 0,87 m (decisión 23). Un panel de 0,87 m libres
        // (ejes x = 0 y 1,17 m) con una viga corta que entra desde la derecha hasta x = 0,40 m.
        List<BordePanel> bordes =
        [
            new("abajo", new(0, 0), new(1170, 0), 300),
            new("arriba", new(0, 3000), new(1170, 3000), 300),
            new("izq", new(0, 0), new(0, 3000), 300),
            new("der", new(1170, 0), new(1170, 3000), 300),
            new("corta", new(1170, 1500), new(400, 1500), 200),
        ];
        Panel panel = DetectorPaneles.Detectar(bordes.Take(4)).Single();

        PropuestaViguetas p = PlanificadorViguetas.Planificar([panel], Direccion.Y, 800, 100, new HashSet<string>(), bordes)!;

        // El parejo con la de la punta (eje en 0,45 m) daba dos a 0,27: no se acepta. Va la fórmula: una al centro.
        Assert.Equal([585.0], p.Viguetas.Select(v => Math.Round(v.Cota)).Distinct());
    }

    [Fact]
    public void UnLadoCasiDerechoSeEnderezaYSuViguetaDeBordeNoVaEnDiagonal()
    {
        // Reunión del 05-nov: «no existen diagonales… la vigueta debe ir derechita» (decisión 20). El punto de arriba a la
        // izquierda quedó 6 cm más alto que el de la derecha.
        PisoViguetas piso = PisoDe(Cuadro("abajo", "der", "izq"));

        Panel z = Assert.Single(piso.Zonas([new ZonaPuntos("Z1", [new(0, 0), new(4000, 0), new(4000, 3000), new(0, 3060)], [])], 100));

        (Punto desde, Punto hasta) = Assert.Single(z.ViguetasDeBorde);
        Assert.Equal(desde.Y, hasta.Y, 3);
        Assert.Equal(3030 - 50, desde.Y, 3);
        Assert.All(z.Eje.Zip(z.Eje.Skip(1).Append(z.Eje[0])), l => Assert.True(Math.Abs(l.First.X - l.Second.X) < 1e-6 || Math.Abs(l.First.Y - l.Second.Y) < 1e-6));
    }

    [Fact]
    public void LasNuevasSiguenALasCreadasDeAlLadoYRellenanLoQueFalta()
    {
        // Captura del 06-nov (voladizo creado después): arriba ya hay dos viguetas creadas por el add-in (x = 1,00 y
        // 1,80 m); se pide solo el panel de abajo. Las nuevas siguen esas líneas y, donde no llegan, la fórmula
        // (decisión 26): antes quedaban solo las dos y 2,00 m sin viguetas a la derecha.
        PisoViguetas piso = ColumnaDeTres();
        var conCreadas = new PisoViguetas(piso.Piso with
        {
            Armazon = [.. piso.Piso.Armazon, .. new[] { 1000, 1800 }.Select(x => new ArmazonPiso($"c{x}", new(x, 6000), new(x, 9000), 100, "VT_10X35", true))],
        }, false);
        Panel abajo = conCreadas.Paneles.Single(p => Geometria.Centro(p.Libre).Y < 3000);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([abajo], Direccion.Y, 800, 100, new HashSet<string>(), conCreadas.Cruces,
            conCreadas.Guias(Direccion.Y, [abajo]))!;

        // Libre de 0,15 a 3,85 m: 1,00 y 1,80 como arriba; a la izquierda quedan 0,80 (más que el máximo de 0,70): una al
        // centro, 0,55; a la derecha, 2,00: dos con la fórmula, 2,45 y 3,25.
        Assert.Equal([550.0, 1000.0, 1800.0, 2450.0, 3250.0], p.Viguetas.Select(v => Math.Round(v.Cota)).Distinct().Order());
        Assert.Equal(OrigenReparto.Existentes, Assert.Single(p.Paneles).Origen);
    }

    [Fact]
    public void ElegidoUnPanelLaViguetaSigueHastaAbajoPorLosDemas()
    {
        // Revisión del 08-nov, fila 6 (decisión 28): se elige solo el panel de arriba y las viguetas llegan hasta abajo.
        PisoViguetas piso = ColumnaDeTres();
        Panel arriba = piso.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);
        List<Panel> resto = piso.Paneles.Where(p => p != arriba).ToList();

        PropuestaViguetas p = PlanificadorViguetas.Planificar([arriba], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces,
            prolongarPor: resto, lineasExistentes: piso.LineasExistentes(Direccion.Y, [arriba]))!;

        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal(4, lineas.Count);
        Assert.All(lineas, l => Assert.Equal((0.0, 9000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
        Assert.Single(p.Tipos);
        // Sin prolongar («Solo P6»), solo el panel elegido.
        PropuestaViguetas solo = PlanificadorViguetas.Planificar([arriba], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces)!;
        Assert.All(solo.Lineas(continuas: true), l => Assert.Equal((6000.0, 9000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
    }

    [Fact]
    public void LaViguetaQueSigueParaDondeYaHayOtraMuyCerca()
    {
        // Decisión 28: en el panel del medio ya hay una vigueta en x = 1,70 m. La de 1,60 m queda a menos de medio eje a
        // eje: para en la viga; las demás siguen hasta abajo.
        PisoViguetas piso = ColumnaDeTres();
        var conVigueta = new PisoViguetas(piso.Piso with
        {
            Armazon = [.. piso.Piso.Armazon, new ArmazonPiso("vt", new(1700, 3000), new(1700, 6000), 100, "VT_10X35", false)],
        }, false);
        Panel arriba = conVigueta.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([arriba], Direccion.Y, 800, 100, new HashSet<string>(), conVigueta.Cruces,
            prolongarPor: conVigueta.Paneles.Where(q => q != arriba), lineasExistentes: conVigueta.LineasExistentes(Direccion.Y, [arriba]))!;

        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal((6000.0, 9000.0), lineas.Where(l => Math.Round(l.Desde.X) == 1600).Select(l => (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))).Single());
        Assert.All(lineas.Where(l => Math.Round(l.Desde.X) != 1600), l => Assert.Equal((0.0, 9000.0), (Math.Round(l.Desde.Y), Math.Round(l.Hasta.Y))));
    }

    [Fact]
    public void UnVacioTambienSeAplicaAUnPanelOPerimetro()
    {
        // Revisión del 08-nov, fila 7 (decisión 29): un vacío marcado en el modo Paneles o Por vigas funciona como en Puntos.
        PisoViguetas piso = ColumnaDeTres();
        Panel arriba = piso.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);

        Panel conVacio = Assert.Single(piso.ConVacios([arriba], [Rectangulo(1500, 7000, 2500, 8000)], 100));
        PropuestaViguetas p = PlanificadorViguetas.Planificar([conVacio], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        Assert.Equal(arriba.Id, conVacio.Id);
        Assert.Equal(4, p.DeBorde.Count);
        // Ninguna vigueta pasa por el vacío (libre de y = 6,90 a 8,10 m, de x = 1,40 a 2,60 m).
        Assert.DoesNotContain(p.Viguetas, v => v.Cota > 1400 && v.Cota < 2600 && v.Desde < 8100 && v.Hasta > 6900);
        // Un vacío fuera del panel no lo cambia.
        Assert.Same(arriba, Assert.Single(piso.ConVacios([arriba], [Rectangulo(1500, 1000, 2500, 2000)], 100)));
    }

    [Fact]
    public void AlVolverVacioUnPanelSeCortanLasViguetasQueLoCruzanYNoLasDeSuBorde()
    {
        // Decisión 30 (como las capturas del 09-nov): una creada y una del modelo cruzan los tres paneles; otra del modelo va
        // solo por el del medio; y una vigueta de borde del modelo va justo por el borde de abajo del panel del medio.
        PisoViguetas piso = ColumnaDeTres();
        var con = new PisoViguetas(piso.Piso with
        {
            Armazon =
            [
                .. piso.Piso.Armazon,
                new ArmazonPiso("creada", new(1000, 0), new(1000, 9000), 100, "VT_10X35", true),
                new ArmazonPiso("modelo", new(2000, 0), new(2000, 9000), 100, "VT_10X35", false),
                new ArmazonPiso("sola", new(3000, 3000), new(3000, 6000), 100, "VT_10X35", false),
                new ArmazonPiso("borde", new(0, 3000), new(4000, 3000), 100, "VB_10X35", false),
            ],
        }, false);
        Panel medio = con.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        var cortes = con.TramosEnPanel(medio);

        Assert.Equal(["creada", "modelo", "sola"], cortes.Select(c => c.Vigueta.Id).Order());
        // Las que siguen quedan de 0 a 3,00 m y de 6,00 a 9,00 m, hasta el eje de las vigas; la que iba solo ahí se borra.
        (ArmazonPiso creada, List<(double Desde, double Hasta)> restos) = cortes.Single(c => c.Vigueta.Id == "creada");
        Assert.Equal([(0.0, 3000.0), (6000.0, 9000.0)], restos.Select(t => (Math.Round(creada.En(t.Desde).Y), Math.Round(creada.En(t.Hasta).Y))));
        Assert.Empty(cortes.Single(c => c.Vigueta.Id == "sola").Restos);
    }

    [Fact]
    public void UnPanelVacioDentroDeUnPerimetroParteLasViguetas()
    {
        // Decisión 30: el panel del medio, marcado como vacío, dentro del perímetro de la columna entera.
        PisoViguetas piso = ColumnaDeTres();
        Panel medio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);
        Panel z = Assert.Single(piso.Perimetro(["izq", "der", "h0", "h9000"]));

        Panel conVacio = Assert.Single(piso.ConVacios([z], [medio.Eje], 100));
        PropuestaViguetas p = PlanificadorViguetas.Planificar([conVacio], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        Assert.Empty(conVacio.ViguetasDeBorde);
        Assert.DoesNotContain(p.Viguetas, v => v.Desde < 6000 && v.Hasta > 3000);
        List<(Punto Desde, Punto Hasta)> lineas = p.Lineas(continuas: true);
        Assert.Equal(4, lineas.Count(l => Math.Round(l.Desde.Y) == 0 && Math.Round(l.Hasta.Y) == 3000));
        Assert.Equal(4, lineas.Count(l => Math.Round(l.Desde.Y) == 6000 && Math.Round(l.Hasta.Y) == 9000));
    }

    [Fact]
    public void UnVacioQueOcupaTodoLoElegidoNoDejaViguetas()
    {
        // Decisión 30: en Por vigas, el perímetro puede ser justo el panel que se vuelve vacío.
        PisoViguetas piso = ColumnaDeTres();
        Panel medio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        List<Panel> quedan = piso.ConVacios([medio], [medio.Eje], 100);

        Assert.Empty(quedan);
        Assert.Null(PlanificadorViguetas.Planificar(quedan, Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, piso.Guias(Direccion.Y, quedan), piso.PiezasDelModelo(quedan).Select(x => (x.Inicio, x.Fin)), piso.Vigas));
    }

    /// <summary>La columna de tres paneles con un voladizo arriba y otro abajo, cerrados por viguetas de borde del modelo.</summary>
    private static PisoViguetas ColumnaConVoladizos()
    {
        List<ArmazonPiso> armazon =
        [
            new("izq", new(0, -1500), new(0, 10500), 300, "V30X40", false),
            new("der", new(4000, -1500), new(4000, 10500), 300, "V30X40", false),
            new("borde-abajo", new(0, -1500), new(4000, -1500), 100, "VT_10X35", false),
            new("borde-arriba", new(0, 10500), new(4000, 10500), 100, "VT_10X35", false),
        ];
        armazon.AddRange(new[] { 0, 3000, 6000, 9000 }.Select(y => new ArmazonPiso($"h{y}", new(0, y), new(4000, y), 300, "V30X40", false)));
        return new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);
    }

    private static PropuestaViguetas ProlongarDesde(PisoViguetas piso, Panel elegido) =>
        PlanificadorViguetas.Planificar([elegido], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas,
            prolongarPor: piso.Paneles.Where(q => q != elegido), lineasExistentes: piso.LineasExistentes(Direccion.Y, [elegido]), voladizos: piso.Voladizos)!;

    [Fact]
    public void LaViguetaDeUnPanelLlegaHastaLosVoladizos()
    {
        // Decisión 31 (captura del 09-nov): los voladizos los cierran viguetas de borde, no son paneles.
        PisoViguetas piso = ColumnaConVoladizos();
        Panel arriba = piso.Paneles.Single(p => Geometria.Centro(p.Libre).Y is > 6000 and < 9000);

        Assert.Equal(3, piso.Paneles.Count);
        Assert.Equal(2, piso.Voladizos.Count);
        Assert.Empty(new PisoViguetas(piso.Piso, true).Voladizos);

        PropuestaViguetas p = ProlongarDesde(piso, arriba);

        Assert.Equal(p.Paneles[0].Reparto.Cantidad, p.Lineas(continuas: true).Count(l =>
            Math.Round(Math.Min(l.Desde.Y, l.Hasta.Y)) == -1500 && Math.Round(Math.Max(l.Desde.Y, l.Hasta.Y)) == 10500));
    }

    [Fact]
    public void UnaViguetaQueYaSiguePorSuLineaNoImpideLlegarAlVoladizo()
    {
        // Decisión 31: la creada va por los dos paneles de abajo; no se repite ahí, pero el voladizo de abajo sí se completa.
        PisoViguetas sinCreada = ColumnaConVoladizos();
        Panel arriba = sinCreada.Paneles.Single(p => Geometria.Centro(p.Libre).Y is > 6000 and < 9000);
        double cota = Math.Round(ProlongarDesde(sinCreada, arriba).Lineas(continuas: true)[0].Desde.X, 3);
        var piso = new PisoViguetas(sinCreada.Piso with
        {
            Armazon = [.. sinCreada.Piso.Armazon, new ArmazonPiso("creada", new(cota, 0), new(cota, 6000), 100, "VT_10X35", true)],
        }, false);

        List<(double Desde, double Hasta)> enLaLinea = ProlongarDesde(piso, arriba).Lineas(continuas: true)
            .Where(l => Math.Abs(l.Desde.X - cota) < 1)
            .Select(l => (Math.Round(Math.Min(l.Desde.Y, l.Hasta.Y)), Math.Round(Math.Max(l.Desde.Y, l.Hasta.Y))))
            .OrderBy(t => t.Item1).ToList();

        Assert.Equal([(-1500.0, 0.0), (6000.0, 10500.0)], enLaLinea);
    }

    [Fact]
    public void UnPanelVacioSoloQuitaEsePanelYLaViguetaSigueDelOtroLado()
    {
        // Decisión 33: se elige el de arriba y el del medio es el vacío; el de abajo y su voladizo conservan la vigueta.
        PisoViguetas piso = ColumnaConVoladizos();
        Panel arriba = piso.Paneles.Single(p => Geometria.Centro(p.Libre).Y is > 6000 and < 9000);
        Panel vacio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        PropuestaViguetas p = PlanificadorViguetas.Planificar([arriba], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas,
            prolongarPor: piso.Paneles.Where(q => q != arriba && q != vacio), lineasExistentes: piso.LineasExistentes(Direccion.Y, [arriba]),
            voladizos: piso.Voladizos, saltar: [vacio])!;

        List<(double Desde, double Hasta)> tramos = p.Lineas(continuas: true)
            .Select(l => (Math.Round(Math.Min(l.Desde.Y, l.Hasta.Y)), Math.Round(Math.Max(l.Desde.Y, l.Hasta.Y)))).ToList();
        int n = p.Paneles[0].Reparto.Cantidad;
        Assert.Equal(2 * n, tramos.Count);
        Assert.Equal(n, tramos.Count(t => t == (6000.0, 10500.0)));
        Assert.Equal(n, tramos.Count(t => t == (-1500.0, 3000.0)));
    }

    [Fact]
    public void EnOtroPisoSeCopianLasViguetasYSeCuentanLasQueNoTienenApoyo()
    {
        // Decisión 34: la propuesta del piso abierto se lleva a otro piso con las mismas vigas, menos una, y viguetas ya puestas.
        PisoViguetas abierto = ColumnaDeTres();
        Panel arriba = abierto.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);
        var otro = new PisoViguetas(abierto.Piso with
        {
            Armazon =
            [
                .. abierto.Piso.Armazon.Where(a => a.Id != "h0"),
                new ArmazonPiso("modelo", new(2000, 6000), new(2000, 9000), 100, "VT_10X35", false),
                new ArmazonPiso("anterior", new(3000, 6000), new(3000, 9000), 100, "VT_10X35", true),
            ],
        }, false);
        (Punto Desde, Punto Hasta)[] lineas =
        [
            (new(1000, 6000), new(1000, 9000)),
            (new(2000, 6000), new(2000, 9000)),
            (new(3000, 6000), new(3000, 9000)),
            (new(3500, 0), new(3500, 3000)),
        ];

        CopiaEnPiso copia = otro.Copiar(lineas, otro.RecortesDeCopia([arriba], [], []), 800);

        // La de 1,00 tiene lugar; la de 3,00 reemplaza a la creada; la de 2,00 choca con la del modelo; la de 3,50 no tiene viga abajo, pero se crea igual.
        Assert.Equal([1000.0, 3000.0, 3500.0], copia.Lineas.Select(l => l.Desde.X));
        Assert.Equal(1, copia.Ocupadas);
        Assert.Equal(1, copia.SinApoyo);
        // Si la del modelo estaba marcada para quitar en el piso abierto, ya no estorba.
        CopiaEnPiso sinModelo = otro.Copiar(lineas, otro.RecortesDeCopia([arriba], [], [(new(2000, 6000), new(2000, 9000))]), 800);
        Assert.Equal([1000.0, 2000.0, 3000.0, 3500.0], sinModelo.Lineas.Select(l => l.Desde.X));
    }

    [Fact]
    public void EnOtroPisoElVacioCortaLoMismoYUnaViguetaSeRecortaUnaSolaVez()
    {
        // Decisión 34: la creada cruza los tres paneles; se reemplaza el de arriba y el del medio es un vacío.
        PisoViguetas columna = ColumnaDeTres();
        var piso = new PisoViguetas(columna.Piso with
        {
            Armazon =
            [
                .. columna.Piso.Armazon,
                new ArmazonPiso("creada", new(1000, 0), new(1000, 9000), 100, "VT_10X35", true),
                new ArmazonPiso("modelo", new(2000, 3000), new(2000, 6000), 100, "VT_10X35", false),
            ],
        }, false);
        Panel arriba = piso.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);
        Panel medio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        var recortes = piso.RecortesDeCopia([arriba], [medio.Eje], []);

        (ArmazonPiso creada, List<(double Desde, double Hasta)> restos) = recortes.Single(x => x.Vigueta.Id == "creada");
        Assert.Equal([(0.0, 3000.0)], restos.Select(t => (Math.Round(creada.En(t.Desde).Y), Math.Round(creada.En(t.Hasta).Y))));
        Assert.Empty(recortes.Single(x => x.Vigueta.Id == "modelo").Restos);
    }

    [Fact]
    public void ElMensajeDeCrearTraeLosPisosYLosVaciados()
    {
        CrearViguetas c = Assert.IsType<CrearViguetas>(ProtocoloViguetas.Leer(
            """{"tipo":"crear","paneles":["P2"],"direccion":"Y","ejeAEje":800,"ancho":100,"tipoVigueta":"123","pisos":["1","2"],"vaciados":[[[0,0],[0,10],[10,10],[10,0]]]}"""));
        Assert.Equal(new[] { "1", "2" }, c.Pisos);
        Assert.Equal(4, Assert.Single(c.LosVaciados).Count);
        // Con solo vacíos marcados, sin paneles elegidos, también vale.
        Assert.IsType<CrearViguetas>(ProtocoloViguetas.Leer("""{"tipo":"crear","tipoVigueta":"123","pisos":["2"],"vaciados":[[[0,0],[0,10],[10,10],[10,0]]]}"""));
    }

    [Fact]
    public void ConDosVaciosEnPanelesDistintosLasViguetasNoSeEnciman()
    {
        // Decisión 35 (fila 9 de la revisión del 09-nov): antes salían líneas a 5 cm y una vigueta distinta por panel.
        List<ArmazonPiso> armazon =
        [
            new("x0", new(0, 0), new(0, 10000), 300, "V30X40", false),
            new("x1", new(9000, 0), new(9000, 10000), 300, "V30X40", false),
        ];
        armazon.AddRange(new[] { 0, 1500, 5800, 10000 }.Select(y => new ArmazonPiso($"y{y}", new(0, y), new(9000, y), 300, "V30X40", false)));
        var piso = new PisoViguetas(new PisoLeido("1", "N+3.70", 3700, armazon, []), false);
        Panel z = piso.Zonas([new ZonaPuntos("Z1", Rectangulo(0, 0, 9000, 10000), [Rectangulo(4500, 2500, 5100, 3500), Rectangulo(6000, 7000, 6900, 8000)])], 100)[0];

        PropuestaViguetas p = PlanificadorViguetas.Planificar([z], Direccion.Y, 800, 100, new HashSet<string>(), piso.Cruces, divisiones: piso.Vigas)!;

        List<(double X, double Y0, double Y1)> nervios = p.Lineas(continuas: true).Where(l => Math.Abs(l.Desde.X - l.Hasta.X) < 1)
            .Select(l => (l.Desde.X, Math.Min(l.Desde.Y, l.Hasta.Y), Math.Max(l.Desde.Y, l.Hasta.Y))).OrderBy(l => l.X).ThenBy(l => l.Item2).ToList();
        foreach ((double X, double Y0, double Y1) a in nervios)
            Assert.DoesNotContain(nervios, b => b != a && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) > 1 && Math.Abs(a.X - b.X) < 400);
        // La línea del borde de un vacío sigue entera por las vigas, de punta a punta.
        Assert.Contains((4450.0, 0.0, 10000.0), nervios.Select(n => (Math.Round(n.X), Math.Round(n.Y0), Math.Round(n.Y1))));
        Assert.Contains((6950.0, 0.0, 10000.0), nervios.Select(n => (Math.Round(n.X), Math.Round(n.Y0), Math.Round(n.Y1))));
        Assert.All(p.Paneles, r => Assert.True(r.Reparto.EjeAEje >= 400, $"eje a eje de {r.Reparto.EjeAEje}"));
    }

    [Fact]
    public void LaListaDePisosDiceCuantasViguetasCreadasTiene()
    {
        // Decisión 36.
        using var json = System.Text.Json.JsonDocument.Parse(ProtocoloViguetas.Pisos("Modelo", [new PisoResumen("1", "N+3.70", 3700, 20, 7), new PisoResumen("2", "N+6.70", 6700, 20)], []));
        Assert.Equal(7, json.RootElement.GetProperty("pisos")[0].GetProperty("creadas").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("pisos")[1].GetProperty("creadas").GetInt32());
    }

    [Fact]
    public void QuitarEnUnPanelSoloCortaElTramoDeEsePanel()
    {
        // Decisión 32: la larga cruza los tres paneles y sigue afuera; la corta está toda dentro del panel del medio.
        PisoViguetas base0 = ColumnaDeTres();
        var piso = new PisoViguetas(base0.Piso with
        {
            Armazon =
            [
                .. base0.Piso.Armazon,
                new ArmazonPiso("larga", new(1000, 0), new(1000, 9000), 100, "VT_10X35", true),
                new ArmazonPiso("corta", new(2000, 3000), new(2000, 6000), 100, "VT_10X35", true),
            ],
        }, false);
        Panel medio = piso.Paneles.Single(p => Math.Abs(Geometria.Centro(p.Libre).Y - 4500) < 1);

        var recortes = piso.CreadasARecortar([medio]);

        List<(double Desde, double Hasta)> larga = recortes.Single(x => x.Vigueta.Id == "larga").Restos;
        Assert.Equal(2, larga.Count);
        Assert.Equal(1.0 / 3, larga[0].Hasta, 3);
        Assert.Equal(2.0 / 3, larga[1].Desde, 3);
        Assert.Empty(recortes.Single(x => x.Vigueta.Id == "corta").Restos);
        // Lo que queda afuera sigue contando; lo que se quita, no.
        Assert.Equal([1000.0], piso.Guias(Direccion.Y, [medio]));
        Assert.Equal([0.0, 6000.0], piso.LineasExistentes(Direccion.Y, [medio]).Where(l => Math.Abs(l.Cota - 1000) < 1).Select(l => Math.Round(l.Desde)).OrderBy(d => d));
        Assert.DoesNotContain(piso.LineasExistentes(Direccion.Y, [medio]), l => Math.Abs(l.Cota - 2000) < 1);
    }

    [Fact]
    public void LasCreadasDentroDeLoElegidoNoSonGuia()
    {
        // Reunión del 05-nov: al reemplazar, las nuevas seguían a las que se iban a borrar (decisión 21).
        PisoViguetas piso = ColumnaDeTres();
        var conCreadas = new PisoViguetas(piso.Piso with
        {
            Armazon =
            [
                .. piso.Piso.Armazon,
                new ArmazonPiso("adentro", new(1000, 6000), new(1000, 9000), 100, "VT_10X35", true),
                new ArmazonPiso("modelo", new(2200, 6000), new(2200, 9000), 100, "VT_10X35", false),
            ],
        }, false);
        Panel arriba = conCreadas.Paneles.Single(p => Geometria.Centro(p.Libre).Y > 6000);

        Assert.Equal([1000.0, 2200.0], conCreadas.Guias(Direccion.Y));
        Assert.Equal([2200.0], conCreadas.Guias(Direccion.Y, [arriba]));
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
        Assert.Equal("P4", Assert.IsType<VaciarPanel>(ProtocoloViguetas.Leer("""{"tipo":"vaciar","id":"P4"}""")).Id);
        // #954, decisiones 28 y 29: prolongar y vacíos en el pedido.
        PedirPropuesta conTodo = Assert.IsType<PedirPropuesta>(ProtocoloViguetas.Leer(
            """{"tipo":"propuesta","paneles":["P1"],"direccion":"Y","ejeAEje":800,"ancho":100,"prolongar":true,"vacios":[[[0,0],[0,10],[10,10],[10,0]]]}"""));
        Assert.True(conTodo.Prolongar);
        Assert.Equal(4, Assert.Single(conTodo.LosVacios).Count);
        Assert.False(c.Propuesta.Prolongar);
    }
}
