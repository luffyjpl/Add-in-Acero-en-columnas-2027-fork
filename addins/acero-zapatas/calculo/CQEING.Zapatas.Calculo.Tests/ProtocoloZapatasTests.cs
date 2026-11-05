using System.Text.Json;

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
        Assert.Equal("99", a.FormaX);
        Assert.Equal("7", a.GanchoY);
    }

    [Fact]
    public void El_recubrimiento_que_no_llega_es_el_de_7_5_cm()
    {
        var c = Assert.IsType<CalcularZapatas>(ProtocoloZapatas.Leer("""{"tipo":"calcular","ids":["1"],"x":{"numero":5,"separacion":150},"y":{"numero":5,"separacion":150}}"""));

        Assert.Equal(Recubrimientos.Global(75), c.Config.Recubrimientos);
        Assert.True(c.Config.IncluirSuperior);
        Assert.Equal(Terminacion.Pata90, c.Config.X.Terminacion);
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
}
