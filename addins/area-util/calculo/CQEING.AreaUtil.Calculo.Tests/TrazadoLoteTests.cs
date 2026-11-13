namespace CQEING.AreaUtil.Calculo.Tests;

/// <summary>El dibujo libre del lote, sin Revit (decisión 21 del plan, S8 #339).</summary>
public class TrazadoLoteTests
{
    [Fact]
    public void Cada_clic_agrega_un_punto()
    {
        var t = new TrazadoLote();
        Assert.Equal(PasoTrazado.Agregado, t.Agregar(new Punto(0, 0)));
        Assert.Equal(PasoTrazado.Agregado, t.Agregar(new Punto(7, 0)));
        Assert.Equal(2, t.Puntos.Count);
    }

    [Fact]
    public void Un_clic_sobre_el_punto_anterior_se_ignora()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        Assert.Equal(PasoTrazado.Repetido, t.Agregar(new Punto(0.0005, 0)));
        Assert.Single(t.Puntos);
    }

    [Fact]
    public void Con_tres_puntos_un_clic_cerca_del_primero_cierra_el_lote()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        t.Agregar(new Punto(7, 14));
        Assert.Equal(PasoTrazado.Cerrado, t.Agregar(new Punto(0.05, 0.05)));
        Assert.Equal(3, t.Puntos.Count);
        Assert.True(t.Cerrado);
    }

    [Fact]
    public void Con_dos_puntos_un_clic_en_el_primero_no_cierra()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        Assert.Equal(PasoTrazado.Agregado, t.Agregar(new Punto(0.05, 0)));
        Assert.False(t.Cerrado);
    }

    [Fact]
    public void Un_clic_a_mas_de_10_cm_del_primero_no_cierra()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        t.Agregar(new Punto(7, 14));
        Assert.Equal(PasoTrazado.Agregado, t.Agregar(new Punto(0.2, 0)));
    }

    [Fact]
    public void Deshacer_quita_el_ultimo_punto_y_sin_puntos_no_hace_nada()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        Assert.True(t.Deshacer());
        Assert.Single(t.Puntos);
        Assert.True(t.Deshacer());
        Assert.False(t.Deshacer());
        Assert.Empty(t.Puntos);
    }

    [Fact]
    public void Se_puede_terminar_solo_con_tres_puntos_o_mas()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        Assert.False(t.PuedeTerminar);
        t.Agregar(new Punto(7, 14));
        Assert.True(t.PuedeTerminar);
    }

    [Fact]
    public void Despues_de_cerrar_no_se_agregan_mas_puntos()
    {
        var t = new TrazadoLote();
        t.Agregar(new Punto(0, 0));
        t.Agregar(new Punto(7, 0));
        t.Agregar(new Punto(7, 14));
        t.Agregar(new Punto(0, 0));
        Assert.Equal(PasoTrazado.Cerrado, t.Agregar(new Punto(3, 3)));
        Assert.Equal(3, t.Puntos.Count);
    }

    [Fact]
    public void Dibujar_en_revit_trae_el_nivel() =>
        Assert.Equal("311", Assert.IsType<DibujarLoteAreaUtil>(ProtocoloAreaUtil.Leer("{\"tipo\":\"dibujarLote\",\"nivelId\":\"311\"}")).NivelId);

    [Fact]
    public void La_respuesta_del_dibujo_lleva_los_puntos()
    {
        string json = ProtocoloAreaUtil.PuntosDibujados([new PuntoLote(1.5, 2, 0), new PuntoLote(8.5, 2, 0)]);
        Assert.Contains("\"tipo\":\"dibujo\"", json);
        Assert.Contains("\"x\":1.5", json);
        Assert.Contains("\"x\":8.5", json);
    }
}
