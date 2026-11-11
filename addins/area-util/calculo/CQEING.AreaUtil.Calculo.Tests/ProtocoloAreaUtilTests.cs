namespace CQEING.AreaUtil.Calculo.Tests;

public class ProtocoloAreaUtilTests
{
    [Fact]
    public void Calcular_un_rectangulo()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"calcular","modo":"rectangulo","frente":7,"fondo":14,"ladoFrente":0,"retiros":{"frontal":1.5,"posterior":2,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{"frenteMinimo":7}}""");
        var c = Assert.IsType<CalcularAreaUtil>(m);
        Assert.Equal(4, c.Entrada.Puntos.Count);
        Assert.Equal(7, c.Entrada.Ficha.FrenteMinimo);
        Assert.Equal(52.5, CalculoLote.Calcular(c.Entrada).Resumen!.AreaUtil, 6);
    }

    [Fact]
    public void Calcular_por_puntos_con_tipos_a_mano()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"calcular","modo":"puntos","puntos":[{"x":0,"y":0,"z":1},{"x":7,"y":0},{"x":7,"y":14},{"x":0,"y":14}],"ladoFrente":2,"tipos":["Posterior","LateralDerecho","Frente","LateralIzquierdo"],"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{}}""");
        var c = Assert.IsType<CalcularAreaUtil>(m);
        Assert.Equal(TipoLado.Frente, c.Entrada.Tipos![2]);
        Assert.Equal(2, c.Entrada.Frente);
    }

    [Fact]
    public void Generar_trae_el_nivel_las_coordenadas_y_los_estilos()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"puntos","puntos":[{"x":0,"y":0},{"x":7,"y":0},{"x":7,"y":14}],"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{},"nivelId":"311","coordenadas":"compartidas","estilos":[{"clave":"lote","nombre":"CQ Lote","color":"#000000","grosor":5}]}""");
        var g = Assert.IsType<GenerarAreaUtil>(m);
        Assert.Equal("311", g.NivelId);
        Assert.True(g.Compartidas);
        Assert.Equal("CQ Lote", g.Estilos[0].Nombre);
    }

    [Fact]
    public void Frente_por_fondo_siempre_va_en_coordenadas_del_proyecto() =>
        Assert.False(Assert.IsType<GenerarAreaUtil>(ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"rectangulo","frente":7,"fondo":14,"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"nivelId":"1","coordenadas":"compartidas"}""")).Compartidas);

    [Fact]
    public void Sin_estilos_usa_los_de_siempre() =>
        Assert.Equal(EstilosLinea.PorDefecto, Assert.IsType<GenerarAreaUtil>(ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"rectangulo","frente":7,"fondo":14,"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"nivelId":"1"}""")).Estilos);

    [Fact]
    public void Un_json_roto_es_desconocido() => Assert.IsType<DesconocidoAreaUtil>(ProtocoloAreaUtil.Leer("{no es json"));

    [Fact]
    public void El_resultado_lleva_areas_trazos_y_avisos()
    {
        string json = ProtocoloAreaUtil.Resultado(CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, null, new Retiros(1.5, 2, 1, 1), new Ficha())));
        Assert.Contains("\"tipo\":\"resultado\"", json);
        Assert.Contains("\"areaUtil\":52.5", json);
        Assert.Contains("\"estilo\":\"area-util\"", json);
        Assert.Contains("\"sePuedeGenerar\":true", json);
    }
}
