using System.Text.Json;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Tablero;

namespace CQEING.Zapatas.Calculo.Tests;

public class ProtocoloZapatasTests
{
    [Theory]
    [InlineData("no es json")]
    [InlineData("{}")]
    [InlineData("""{"tipo":"otro"}""")]
    [InlineData("""{"tipo":"calcular"}""")]
    [InlineData("""{"tipo":"mostrar","ids":[]}""")]
    public void Lo_que_no_se_entiende_no_lanza(string json) => Assert.IsType<DesconocidoZapatas>(ProtocoloZapatas.Leer(json));

    [Fact]
    public void Aplicar_trae_la_configuracion_y_las_elecciones()
    {
        MensajeZapatas m = ProtocoloZapatas.Leer("""
            {"tipo":"aplicar","ids":["1","2"],"x":{"numero":5,"separacion":150,"terminacion":"Pata90"},"y":{"numero":4,"separacion":200,"terminacion":"Gancho180"},
             "inferior":70,"lateral":75,"superior":50,"incluirSuperior":false,"formaX":"99","ganchoY":"7"}
            """);

        var a = Assert.IsType<AplicarZapatas>(m);
        Assert.Equal(["1", "2"], a.Ids);
        Assert.Equal(new ConfigDireccion(4, 200, Terminacion.Gancho180), a.Config.Y);
        Assert.Equal(new Recubrimientos(70, 75, 50), a.Config.Recubrimientos);
        Assert.False(a.Config.IncluirSuperior);
        Assert.Equal("99", a.Tipos.FormaX);
        Assert.Equal("7", a.Tipos.GanchoY);
        // Sin los de la superior, la superior usa los de la inferior.
        Assert.Equal("7", a.Tipos.Gancho(Parrilla.Superior, Direccion.Y));
    }

    [Fact]
    public void Calcular_trae_la_parrilla_superior_las_patas_y_el_largo_de_pata()
    {
        var c = Assert.IsType<CalcularZapatas>(ProtocoloZapatas.Leer("""
            {"tipo":"calcular","ids":["1"],"largoPata":"Recubrimiento",
             "x":{"numero":5,"separacion":150,"terminacion":"Pata90","patas":"Inicio","superior":{"terminacion":"Gancho135"}},
             "y":{"numero":5,"separacion":150},"ganchoSuperiorX":"8"}
            """));

        Assert.Equal(LargoPata.Recubrimiento, c.Config.LargoPata);
        Assert.Equal(new Extremos(Terminacion.Pata90, Patas.Inicio), c.Config.X.En(Parrilla.Inferior));
        Assert.Equal(new Extremos(Terminacion.Gancho135), c.Config.X.En(Parrilla.Superior));
        Assert.Equal(new Extremos(), c.Config.Y.En(Parrilla.Superior));
        Assert.Equal("8", c.Tipos.Gancho(Parrilla.Superior, Direccion.X));
        Assert.Null(c.Tipos.Gancho(Parrilla.Inferior, Direccion.X));
    }

    [Fact]
    public void El_recubrimiento_que_no_llega_es_el_de_7_5_cm()
    {
        var c = Assert.IsType<CalcularZapatas>(ProtocoloZapatas.Leer("""{"tipo":"calcular","ids":["1"],"x":{"numero":5,"separacion":150},"y":{"numero":5,"separacion":150}}"""));

        Assert.Equal(Recubrimientos.Global(75), c.Config.Recubrimientos);
        Assert.True(c.Config.IncluirSuperior);
        Assert.Equal(Terminacion.Pata90, c.Config.X.Terminacion);
        Assert.Equal(LargoPata.Cuadro, c.Config.LargoPata);
    }

    [Fact]
    public void Los_datos_salen_en_camelCase_con_los_acentos_sin_escapar()
    {
        var z = new ZapataLeida("10", "Z-1", "A-1", "Cimentación", 0, -1000, 350, [new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], [new ColumnaLeida("20", new(900, 900), 400, 400)], 75, 75, 75);

        string json = ProtocoloZapatas.Datos("Modelo", [z], [["10"]], new CatalogoZapatas([], [], []), new Dictionary<string, (int, int)> { ["10"] = (4, 2) });

        Assert.Contains("Cimentación", json);
        using JsonDocument d = JsonDocument.Parse(json);
        JsonElement zapata = d.RootElement.GetProperty("zapatas")[0];
        Assert.Equal("Individual", zapata.GetProperty("tipo").GetString());
        Assert.Equal("Cuadrada", zapata.GetProperty("forma").GetString());
        Assert.Equal(4, zapata.GetProperty("barrasTotales").GetInt32());
        Assert.Equal(1800, zapata.GetProperty("enModelo")[1].GetProperty("x").GetDouble());
    }

    [Fact]
    public void Aplicar_sin_forzar_es_lo_normal()
    {
        const string Base = """{"tipo":"aplicar","ids":["1"],"x":{"numero":5,"separacion":150},"y":{"numero":5,"separacion":150}""";

        Assert.False(Assert.IsType<AplicarZapatas>(ProtocoloZapatas.Leer(Base + "}")).Forzar);
        Assert.True(Assert.IsType<AplicarZapatas>(ProtocoloZapatas.Leer(Base + ""","forzar":true}""")).Forzar);
    }

    private static readonly CatalogoZapatas Catalogo = new(
        [new BarraCatalogo(5, "#5 CIM", 15.9, 95.25, 278, 0, 0, new Dictionary<string, double> { ["180"] = 79.37 })],
        [new FormaCatalogo("00", "00", [], ClaseForma.Recta), new FormaCatalogo("17A", "17A", [], ClaseForma.L), new FormaCatalogo("17", "17", [], ClaseForma.U)],
        []);

    [Theory]
    [InlineData("00", Patas.Ninguna)]
    [InlineData("17A", Patas.Inicio)]
    [InlineData("17", Patas.Ambas)]
    [InlineData(null, Patas.Ambas)]
    public void Con_pata_de_90_la_forma_decide_las_patas(string? forma, Patas patas)
    {
        var c = new ConfigParrillas(new ConfigDireccion(5, 150), new ConfigDireccion(5, 150), Recubrimientos.Global(75));

        ConfigParrillas r = ProtocoloZapatas.ConTipos(c, new TiposElegidos(forma, null, null, null), Catalogo);

        Assert.Equal(patas, r.X.En(Parrilla.Inferior).Patas);
        // Sin forma propia, la superior usa la de la inferior.
        Assert.Equal(patas, r.X.En(Parrilla.Superior).Patas);
        Assert.Equal(Patas.Ambas, r.Y.Patas);
        Assert.Equal(95.25, r.Doblados![5]);
    }

    [Fact]
    public void Con_gancho_de_180_la_cola_sale_del_catalogo_y_la_forma_no_cuenta()
    {
        var x = new ConfigDireccion(5, 150, Terminacion.Gancho180);
        var c = new ConfigParrillas(x, x, Recubrimientos.Global(75));

        ConfigParrillas r = ProtocoloZapatas.ConTipos(c, new TiposElegidos("17A", null, "180", null, GanchoSuperiorX: "otro"), Catalogo);

        Assert.Equal(new Extremos(Terminacion.Gancho180, Patas.Ambas, 79.37), r.X.En(Parrilla.Inferior));
        // Un gancho sin cola en el catálogo deja la del cuadro (null).
        Assert.Null(r.X.En(Parrilla.Superior).Cola);
        Assert.Null(r.Y.Cola);
    }

    [Fact]
    public void El_resultado_lleva_trazos_con_color_y_choques()
    {
        var x = new ConfigDireccion(5, 150, Terminacion.Gancho135, Cola: 79.37);
        var c = new ConfigParrillas(x, x, Recubrimientos.Global(75), false, new Dictionary<int, double> { [5] = 95.25 });
        ResultadoZapata r = MotorParrillas.Calcular(new ZapataEntrada([new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], 350, []), c, Reglas.Generales);
        var trazos = TrazosZapata.Trazos(r.Conjuntos, Reglas.Generales, c.Doblados);
        var choques = TrazosZapata.Choques(r.Conjuntos, trazos);

        using JsonDocument d = JsonDocument.Parse(ProtocoloZapatas.Resultado(r, ["1"], [], [], trazos, choques));

        JsonElement trazo = d.RootElement.GetProperty("trazos")[0];
        Assert.Equal("0.0", trazo.GetProperty("pieza").GetString());
        Assert.Equal("#EA4335", trazo.GetProperty("color").GetString());
        Assert.Equal(3, trazo.GetProperty("puntos")[0].GetArrayLength());
        Assert.Equal(choques.Count, d.RootElement.GetProperty("choques").GetArrayLength());
        Assert.NotEmpty(choques);
        Assert.StartsWith("La barra", d.RootElement.GetProperty("choques")[0].GetProperty("texto").GetString());
    }

    [Fact]
    public void Los_datos_llevan_la_pantalla_de_reglas_como_vigas()
    {
        using JsonDocument d = JsonDocument.Parse(ProtocoloZapatas.Datos("M", [], [], new CatalogoZapatas([], [], []), new Dictionary<string, (int, int)>()));

        JsonElement reglas = d.RootElement.GetProperty("reglas");
        JsonElement n5 = reglas.GetProperty("porDiametro").EnumerateArray().First(f => f.GetProperty("d").GetString() == "#5");
        // Los valores del cuadro de ganchos (imágenes del 04-nov): #5 a 90° 0.278, 180° 0.238, estribo 0.189.
        Assert.Equal("0.278", n5.GetProperty("g").GetString());
        Assert.Equal("0.238", n5.GetProperty("g180").GetString());
        Assert.Equal("0.189", n5.GetProperty("e").GetString());
    }

    [Fact]
    public void Guardar_reglas_lee_la_pantalla_y_valida_como_vigas()
    {
        VistaReglas pantalla = PantallaReglas.De(new ReglasCargadas(Reglas.Generales, null));
        string json = JsonSerializer.Serialize(new { tipo = "guardarReglas", reglas = pantalla with { PorDiametro = pantalla.PorDiametro.Select(f => f.D == "#5" ? f with { G = "0.300" } : f).ToList() } }, ProtocoloZapatas.Json);

        var g = Assert.IsType<GuardarReglasZapatas>(ProtocoloZapatas.Leer(json));
        (Reglas? nuevas, string? error) = ProtocoloZapatas.ReglasAGuardar(g.Reglas, Reglas.Generales);

        Assert.Null(error);
        Assert.Equal(300, nuevas!.PorDiametro[5].Gancho90.B);
        // Lo que la pantalla no toca queda igual.
        Assert.Equal(Reglas.Generales.PorDiametro[5].Gancho180, nuevas.PorDiametro[5].Gancho180);
        Assert.NotNull(ProtocoloZapatas.ReglasAGuardar(pantalla with { Multiplo = "abc" }, Reglas.Generales).Error);
    }

    [Fact]
    public void Las_reglas_guardadas_cambian_la_pata_del_cuadro()
    {
        // Las reglas compartidas con vigas (decisión 38): con el gancho de 90° del #5 a 300, la pata «según el cuadro» mide 300.
        string ruta = Path.Combine(Path.GetTempPath(), $"reglas-{Guid.NewGuid():N}.json");
        try
        {
            var almacen = new AlmacenReglas(ruta);
            Reglas r = Reglas.Generales;
            almacen.Guardar(r with { PorDiametro = new Dictionary<int, ReglaDiametro>(r.PorDiametro) { [5] = r.PorDiametro[5] with { Gancho90 = r.PorDiametro[5].Gancho90 with { B = 300 } } } });
            Reglas cargadas = almacen.Cargar().Reglas;
            var c = new ConfigParrillas(new ConfigDireccion(5, 150), new ConfigDireccion(5, 150), Recubrimientos.Global(75), false);

            ResultadoZapata z = MotorParrillas.Calcular(new ZapataEntrada([new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], 800, []), c, cargadas);

            Assert.Equal(300, z.Conjuntos[0].Pata, 3);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Los_choques_al_aplicar_dicen_cuantos()
    {
        using JsonDocument d = JsonDocument.Parse(ProtocoloZapatas.Choques(4));

        Assert.Equal("choques", d.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(4, d.RootElement.GetProperty("cantidad").GetInt32());
    }
}
