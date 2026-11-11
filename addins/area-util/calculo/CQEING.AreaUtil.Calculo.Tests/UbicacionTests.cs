namespace CQEING.AreaUtil.Calculo.Tests;

/// <summary>Dónde se coloca el lote de frente × fondo en Revit (decisión 18 del plan, S7 #338).</summary>
public class UbicacionTests
{
    private const string Retiros = "\"retiros\":{\"frontal\":1.5,\"posterior\":2,\"lateralIzquierdo\":1,\"lateralDerecho\":1}";

    private static void Cerca(Punto esperado, Punto real)
    {
        Assert.Equal(esperado.X, real.X, 6);
        Assert.Equal(esperado.Y, real.Y, 6);
    }

    [Fact]
    public void Sin_giro_solo_se_mueve_la_esquina()
    {
        var u = new Ubicacion(100, 50, 0);
        Cerca(new Punto(100, 50), u.Aplicar(new Punto(0, 0)));
        Cerca(new Punto(107, 64), u.Aplicar(new Punto(7, 14)));
    }

    [Fact]
    public void Con_90_grados_el_frente_queda_sobre_el_eje_y()
    {
        var u = new Ubicacion(10, 0, 90);
        Cerca(new Punto(10, 7), u.Aplicar(new Punto(7, 0)));
        Cerca(new Punto(-4, 0), u.Aplicar(new Punto(0, 14)));
    }

    [Fact]
    public void El_origen_no_cambia_nada() => Cerca(new Punto(7, 14), Ubicacion.Origen.Aplicar(new Punto(7, 14)));

    [Theory]
    [InlineData(10, 5, 10, 15, 90)]
    [InlineData(0, 0, 10, 0, 0)]
    [InlineData(0, 0, -3, 0, 180)]
    public void Dos_clics_dan_la_esquina_y_el_giro(double x1, double y1, double x2, double y2, double giro)
    {
        Ubicacion u = Ubicacion.DesdeDosPuntos(new Punto(x1, y1), new Punto(x2, y2));
        Assert.Equal(x1, u.X, 6);
        Assert.Equal(y1, u.Y, 6);
        Assert.Equal(giro, u.GiroGrados, 6);
    }

    [Fact]
    public void Dos_clics_a_30_grados()
    {
        Ubicacion u = Ubicacion.DesdeDosPuntos(new Punto(0, 0), new Punto(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)));
        Assert.Equal(30, u.GiroGrados, 6);
    }

    [Fact]
    public void Dos_clics_en_el_mismo_punto_dejan_el_giro_en_cero() =>
        Assert.Equal(0, Ubicacion.DesdeDosPuntos(new Punto(4, 4), new Punto(4, 4)).GiroGrados);

    [Fact]
    public void Los_giros_negativos_se_pasan_a_entre_0_y_360() =>
        Assert.Equal(270, Ubicacion.DesdeDosPuntos(new Punto(0, 0), new Punto(0, -5)).GiroGrados, 6);

    [Fact]
    public void El_rectangulo_ubicado_y_girado_mide_lo_mismo()
    {
        var m = ProtocoloAreaUtil.Leer("{\"tipo\":\"calcular\",\"modo\":\"rectangulo\",\"frente\":7,\"fondo\":14," + Retiros + ",\"ubicacion\":{\"x\":500,\"y\":-20,\"giro\":37}}");
        EntradaLote e = Assert.IsType<CalcularAreaUtil>(m).Entrada;
        Cerca(new Punto(500, -20), e.Puntos[0]);
        ResultadoLote r = CalculoLote.Calcular(e);
        Assert.Equal(98, r.Resumen!.AreaLote, 6);
        Assert.Equal(52.5, r.Resumen.AreaUtil, 6);
        Assert.Equal(7, r.Resumen.Frente, 6);
    }

    [Fact]
    public void El_modo_puntos_ignora_la_ubicacion()
    {
        var m = ProtocoloAreaUtil.Leer("{\"tipo\":\"calcular\",\"modo\":\"puntos\",\"puntos\":[{\"x\":0,\"y\":0},{\"x\":7,\"y\":0},{\"x\":7,\"y\":14}]," + Retiros + ",\"ubicacion\":{\"x\":500,\"y\":500,\"giro\":45}}");
        Cerca(new Punto(0, 0), Assert.IsType<CalcularAreaUtil>(m).Entrada.Puntos[0]);
    }

    [Fact]
    public void Elegir_en_revit_trae_el_nivel() =>
        Assert.Equal("311", Assert.IsType<ElegirUbicacionAreaUtil>(ProtocoloAreaUtil.Leer("{\"tipo\":\"elegirUbicacion\",\"nivelId\":\"311\"}")).NivelId);

    [Fact]
    public void La_respuesta_lleva_x_y_y_giro()
    {
        string json = ProtocoloAreaUtil.UbicacionElegida(new Ubicacion(12.5, -3, 90));
        Assert.Contains("\"tipo\":\"ubicacion\"", json);
        Assert.Contains("\"x\":12.5", json);
        Assert.Contains("\"giro\":90", json);
    }
}
