namespace CQEING.AreaUtil.Calculo.Tests;

/// <summary>Los retiros arrancan vacíos, sin valores de ejemplo (decisión 24 del plan, punto 2).</summary>
public class RetirosVaciosTests
{
    private static ResultadoLote Calcular(string retiros) => CalculoLote.Calcular(Assert.IsType<CalcularAreaUtil>(
        ProtocoloAreaUtil.Leer("{\"tipo\":\"calcular\",\"modo\":\"rectangulo\",\"frente\":7,\"fondo\":14,\"retiros\":{" + retiros + "}}")).Entrada);

    [Fact]
    public void Un_retiro_vacio_se_toma_como_cero_y_avisa_cual_falta()
    {
        ResultadoLote r = Calcular("\"frontal\":1.5,\"posterior\":null,\"lateralIzquierdo\":1,\"lateralDerecho\":1");
        Assert.True(r.SePuedeGenerar);
        Assert.Equal(5 * 12.5, r.Resumen!.AreaUtil, 6);
        Aviso aviso = Assert.Single(r.Avisos);
        Assert.False(aviso.Bloquea);
        Assert.Contains("posterior", aviso.Texto);
    }

    [Fact]
    public void Todos_vacios_nombra_los_cuatro()
    {
        ResultadoLote r = Calcular("");
        Aviso aviso = Assert.Single(r.Avisos);
        foreach (string lado in new[] { "frontal", "posterior", "lateral izquierdo", "lateral derecho" })
            Assert.Contains(lado, aviso.Texto);
        Assert.Equal(98, r.Resumen!.AreaUtil, 6);
    }

    [Fact]
    public void Un_cero_escrito_no_avisa() =>
        Assert.Empty(Calcular("\"frontal\":0,\"posterior\":0,\"lateralIzquierdo\":0,\"lateralDerecho\":0").Avisos);

    [Fact]
    public void Con_todos_los_retiros_no_avisa() =>
        Assert.Empty(Calcular("\"frontal\":1.5,\"posterior\":2,\"lateralIzquierdo\":1,\"lateralDerecho\":1").Avisos);
}
