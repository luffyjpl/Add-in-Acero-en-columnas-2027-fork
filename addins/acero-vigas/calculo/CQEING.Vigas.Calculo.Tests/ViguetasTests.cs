using CQEING.Vigas.Calculo.Porticos;
using CQEING.Vigas.Calculo.Viguetas;

namespace CQEING.Vigas.Calculo.Tests;

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

        // La vigueta 1 (y = 0,75 m) cruza la viga corta; la 2 (y = 1,55 m) pasa por encima de donde termina.
        Assert.Equal(["P1:1a", "P1:1b", "P1:2"], p.Viguetas.Select(v => v.Id));
        Vigueta a = p.Viguetas[0], b = p.Viguetas[1];
        Assert.Equal((150.0, 1900.0, 0.0, 2000.0), (a.Desde, a.Hasta, a.EjeDesde, a.EjeHasta));
        Assert.Equal((2100.0, 3720.0, 2000.0, 3870.0), (b.Desde, b.Hasta, b.EjeDesde, b.EjeHasta));
        // Continuas: los dos lados van en una sola vigueta; sin continuas, en dos.
        Assert.Equal(2, p.Lineas(continuas: true).Count);
        Assert.Equal(3, p.Lineas(continuas: false).Count);
    }

    [Fact]
    public void BorrarUnLadoDejaLaViguetaHastaLaViga()
    {
        PropuestaViguetas p = PlanificadorViguetas.Planificar([PanelMaqueta()], Direccion.X, 800, 100, new HashSet<string> { "P1:1b" }, [VigaQueCruza])!;

        (Punto desde, Punto hasta) = p.Lineas(continuas: true).Single(l => Math.Round(l.Desde.Y) == 750);
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

        List<double> guias = piso.Guias(Direccion.Y, [abajo]);
        PropuestaViguetas p = PlanificadorViguetas.Planificar([abajo], Direccion.Y, 800, 100, new HashSet<string>(), guias: guias)!;

        Assert.Equal([1000.0, 2200.0], guias);
        RepartoPanel r = Assert.Single(p.Paneles);
        Assert.Equal(OrigenReparto.Existentes, r.Origen);
        Assert.Equal([1000.0, 2200.0], p.Viguetas.Select(v => v.Cota));
        Assert.Equal(1200, r.Reparto.EjeAEje);
        // Una vigueta horizontal no es guía de viguetas en ↕ Y.
        Assert.Empty(piso.Guias(Direccion.X, [abajo]));
    }

    [Fact]
    public void LasViguetasDelModeloConPiezasQuitadasNoSonGuiaYLasVigasNoSonPiezas()
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

        Assert.Equal([2200.0], piso.Guias(Direccion.Y, [abajo], ["vt1"]));
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
