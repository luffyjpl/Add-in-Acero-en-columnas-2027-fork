using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Calculo.Tests;

public class ValoresPorDefectoTests
{
    [Fact]
    public void El_recubrimiento_por_defecto_es_de_7_5_cm() => Assert.Equal(75, ValoresPorDefecto.RecubrimientoMm);
}
