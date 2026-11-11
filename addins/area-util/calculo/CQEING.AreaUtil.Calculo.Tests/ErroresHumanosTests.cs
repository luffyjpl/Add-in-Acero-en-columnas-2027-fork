namespace CQEING.AreaUtil.Calculo.Tests;

/// <summary>Datos mal escritos que no deben romper el cálculo ni dar un resultado sin sentido (decisión 17 del plan).</summary>
public class ErroresHumanosTests
{
    private const string Retiros = "\"retiros\":{\"frontal\":1.5,\"posterior\":2,\"lateralIzquierdo\":1,\"lateralDerecho\":1}";

    private static ResultadoLote CalcularJson(string json) =>
        CalculoLote.Calcular(Assert.IsType<CalcularAreaUtil>(ProtocoloAreaUtil.Leer(json)).Entrada);

    [Fact]
    public void Pisos_con_decimales_no_rompe_el_calculo_y_toma_la_parte_entera()
    {
        ResultadoLote r = CalcularJson("{\"tipo\":\"calcular\",\"modo\":\"rectangulo\",\"frente\":7,\"fondo\":14," + Retiros + ",\"ficha\":{\"imc\":1,\"pisos\":2.5}}");
        Assert.Equal(105, r.Resumen!.ConstruccionPosible!.Value, 6);
    }

    [Theory]
    [InlineData(0, 14)]
    [InlineData(7, 0)]
    [InlineData(-7, 14)]
    [InlineData(7, -14)]
    public void Frente_o_fondo_en_cero_o_negativos_bloquean_y_dicen_que_falta(double frente, double fondo)
    {
        ResultadoLote r = CalcularJson($"{{\"tipo\":\"calcular\",\"modo\":\"rectangulo\",\"frente\":{frente},\"fondo\":{fondo}," + Retiros + "}");
        Assert.False(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Bloquea && a.Texto.Contains("frente y fondo"));
    }

    [Fact]
    public void Datos_de_la_ficha_en_cero_o_negativos_se_ignoran_y_se_avisa()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, null, new Retiros(1.5, 2, 1, 1),
            new Ficha(Imo: -0.5, Imc: 0, Pisos: -1, LoteMinimo: -96, FrenteMinimo: 0)));
        Assert.True(r.SePuedeGenerar);
        Assert.Null(r.Resumen!.OcupacionMaxima);
        Assert.Null(r.Resumen.ConstruccionMaxima);
        Assert.Null(r.Resumen.ConstruccionPosible);
        Assert.Equal(5, r.Avisos.Count(a => a.Texto.Contains("no se tuvo en cuenta") && !a.Bloquea));
    }

    [Fact]
    public void Tres_puntos_alineados_no_son_un_lote() =>
        Assert.False(CalculoLote.Calcular(new EntradaLote([new(0, 0), new(5, 0), new(10, 0)], 0, null, new Retiros(1, 1, 1, 1), new Ficha())).SePuedeGenerar);

    [Fact]
    public void Un_lote_triangular_tambien_deja_area_util()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote([new(0, 0), new(20, 0), new(10, 20)], 0, null, new Retiros(1, 1, 1, 1), new Ficha()));
        Assert.True(r.SePuedeGenerar);
        Assert.InRange(r.Resumen!.AreaUtil, 1, 199);
    }

    [Fact]
    public void Coordenadas_de_excel_con_miles_al_estilo_colombiano()
    {
        var (puntos, errores) = LectorPuntos.Leer("1.200.000,50\t900.000,25\t10,1");
        Assert.Empty(errores);
        Assert.Equal(new PuntoLote(1200000.50, 900000.25, 10.1), puntos[0]);
    }

    [Fact]
    public void Coordenadas_con_miles_al_estilo_ingles()
    {
        var (puntos, errores) = LectorPuntos.Leer("1,200,000.50\t900,000.25");
        Assert.Empty(errores);
        Assert.Equal(new PuntoLote(1200000.50, 900000.25, 0), puntos[0]);
    }

    [Fact]
    public void Un_lote_de_un_solo_punto_repetido_no_es_un_lote() =>
        Assert.False(CalculoLote.Calcular(new EntradaLote([new(3, 3), new(3, 3), new(3, 3), new(3, 3)], 0, null, new Retiros(1, 1, 1, 1), new Ficha())).SePuedeGenerar);
}
