namespace CQEING.Despiece.Calculo.Tests;

/// <summary>Grupo A/B de cada varilla (#228, decisión 4 del plan de columnas). Datos medidos en Revit, hallazgo 15.</summary>
public class GruposTraslapoTests
{
    // Columna de ejes 1 y 6 de "Prueba 08072026": 8 varillas, en el orden en que salieron de Revit (no el del anillo).
    private static readonly double[] TraslapoA = [1.788];
    private static readonly double[] TraslapoB = [5.288];

    private static List<VarillaLeida> Medidas() =>
    [
        new(6.910, -0.078, TraslapoA),
        new(7.000, -0.094, TraslapoB),
        new(7.090, -0.078, TraslapoA),
        new(7.094, 0.000, TraslapoB),
        new(7.090, 0.090, TraslapoA),
        new(7.000, 0.094, TraslapoB),
        new(6.922, 0.090, TraslapoA),
        new(6.906, 0.000, TraslapoB),
    ];

    [Fact]
    public void ElAnilloEmpiezaAbajoALaIzquierdaYVaContraReloj()
    {
        List<VarillaLeida> v = Medidas();
        // Que el orden de entrada no importe.
        v.Reverse();

        IReadOnlyList<int> anillo = GruposTraslapo.Anillo(v);

        Assert.Equal([(6.910, -0.078), (7.000, -0.094), (7.090, -0.078), (7.094, 0.000)], anillo.Take(4).Select(i => (v[i].X, v[i].Y)));
    }

    [Fact]
    public void ConElRegistroSalenIntercaladasYCoincidenConLaAltura()
    {
        List<VarillaLeida> v = Medidas();

        ResultadoGrupos? r = GruposTraslapo.Asignar(v, varillasDelRegistro: 8);

        Assert.NotNull(r);
        Assert.Equal(FuenteGrupos.Registro, r.Fuente);
        Assert.True(r.Coinciden);
        Assert.Equal([GrupoTraslapo.A, GrupoTraslapo.B, GrupoTraslapo.A, GrupoTraslapo.B, GrupoTraslapo.A, GrupoTraslapo.B, GrupoTraslapo.A, GrupoTraslapo.B], r.Grupos);
    }

    [Fact]
    public void SinRegistroSeUsaLaAlturaDeLosTraslapos()
    {
        ResultadoGrupos? r = GruposTraslapo.Asignar(Medidas(), varillasDelRegistro: null);

        Assert.NotNull(r);
        Assert.Equal(FuenteGrupos.Altura, r.Fuente);
        Assert.Equal(GrupoTraslapo.A, r.Grupos[0]);
        Assert.Equal(GrupoTraslapo.B, r.Grupos[1]);
    }

    [Fact]
    public void SiElRegistroTieneOtraCantidadDeVarillasSeUsaLaAltura()
    {
        ResultadoGrupos? r = GruposTraslapo.Asignar(Medidas(), varillasDelRegistro: 12);

        Assert.Equal(FuenteGrupos.Altura, r?.Fuente);
    }

    [Fact]
    public void SiNoCoincidenMandaElRegistro()
    {
        List<VarillaLeida> v = Medidas();
        v[0] = v[0] with { Traslapos = TraslapoB };

        ResultadoGrupos? r = GruposTraslapo.Asignar(v, varillasDelRegistro: 8);

        Assert.NotNull(r);
        Assert.False(r.Coinciden);
        Assert.Equal(GrupoTraslapo.A, r.Grupos[0]);
    }

    [Fact]
    public void SinRegistroNiTraslaposNoHayGrupos()
    {
        List<VarillaLeida> v = Medidas().Select(x => x with { Traslapos = [] }).ToList();

        Assert.Null(GruposTraslapo.Asignar(v, varillasDelRegistro: null));
    }

    [Fact]
    public void UnaDiferenciaDeMilimetrosEnLaAlturaNoCambiaElGrupo()
    {
        List<VarillaLeida> v = Medidas();
        v[2] = v[2] with { Traslapos = [1.790] };

        ResultadoGrupos? r = GruposTraslapo.Asignar(v, varillasDelRegistro: null);

        Assert.Equal(GrupoTraslapo.A, r?.Grupos[2]);
    }
}
