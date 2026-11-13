namespace CQEING.AreaUtil.Calculo.Tests;

/// <summary>
/// El nivel donde va el área útil del primer piso (decisión 23 del plan): con el add-in abierto desde la vista 3D se
/// tomaba el nivel más bajo, la cimentación N-0.65, y las líneas no se veían en la planta del piso 1.
/// </summary>
public class NivelPorDefectoTests
{
    private static readonly NivelLeido Cimentacion = new("1", "N-0.65", -0.65);
    private static readonly NivelLeido Piso1 = new("2", "N+0.00 Piso 1", 0);
    private static readonly NivelLeido Piso2 = new("3", "N+3.00 Piso 2", 3);

    [Fact]
    public void Sin_planta_activa_toma_el_piso_a_cota_cero_y_no_la_cimentacion() =>
        Assert.Equal("2", NivelesLote.PorDefecto([Cimentacion, Piso1, Piso2], null));

    [Fact]
    public void Con_planta_activa_toma_su_nivel() =>
        Assert.Equal("3", NivelesLote.PorDefecto([Cimentacion, Piso1, Piso2], "3"));

    [Fact]
    public void Si_ningun_nivel_esta_en_cota_cero_toma_el_primero_por_encima() =>
        Assert.Equal("2", NivelesLote.PorDefecto([Cimentacion, new NivelLeido("2", "N+0.45", 0.45), Piso2], null));

    [Fact]
    public void Si_todos_estan_bajo_cero_toma_el_mas_alto() =>
        Assert.Equal("2", NivelesLote.PorDefecto([new NivelLeido("1", "Sótano 2", -6), new NivelLeido("2", "Sótano 1", -3)], null));

    [Fact]
    public void Un_nivel_activo_que_no_esta_en_la_lista_se_ignora() =>
        Assert.Equal("2", NivelesLote.PorDefecto([Cimentacion, Piso1], "99"));

    [Fact]
    public void Sin_niveles_no_hay_nivel() => Assert.Null(NivelesLote.PorDefecto([], null));
}
