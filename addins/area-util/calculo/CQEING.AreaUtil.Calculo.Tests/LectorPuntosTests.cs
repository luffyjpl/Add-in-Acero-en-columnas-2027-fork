namespace CQEING.AreaUtil.Calculo.Tests;

public class LectorPuntosTests
{
    [Fact]
    public void Columnas_de_excel_en_espanol_con_tabulador_y_coma_decimal()
    {
        var (puntos, errores) = LectorPuntos.Leer("1200000,50\t900000,25\t10,1\n1200007\t900000\t10");
        Assert.Empty(errores);
        Assert.Equal(new PuntoLote(1200000.50, 900000.25, 10.1), puntos[0]);
        Assert.Equal(new PuntoLote(1200007, 900000, 10), puntos[1]);
    }

    [Fact]
    public void Separado_por_punto_y_coma_o_por_espacios()
    {
        Assert.Equal(new PuntoLote(1.5, 2, 0), LectorPuntos.Leer("1,5;2").Puntos[0]);
        Assert.Equal(new PuntoLote(1.5, 2, 3), LectorPuntos.Leer("1.5   2   3").Puntos[0]);
    }

    [Fact]
    public void Csv_con_punto_decimal_y_coma_como_separador() =>
        Assert.Equal(new PuntoLote(1.5, 2.25, 0), LectorPuntos.Leer("1.5,2.25").Puntos[0]);

    [Fact]
    public void El_encabezado_y_el_rotulo_del_punto_se_ignoran()
    {
        var (puntos, errores) = LectorPuntos.Leer("Punto\tX\tY\tZ\nP1\t10\t20\t3\nP2\t17\t20\t3");
        Assert.Empty(errores);
        Assert.Equal(2, puntos.Count);
        Assert.Equal(new PuntoLote(10, 20, 3), puntos[0]);
    }

    [Fact]
    public void Una_fila_que_no_se_entiende_se_informa_con_su_numero()
    {
        var (puntos, errores) = LectorPuntos.Leer("10\t20\nhola\n17\t20");
        Assert.Equal(2, puntos.Count);
        Assert.Single(errores);
        Assert.Contains("Fila 2", errores[0]);
    }
}
