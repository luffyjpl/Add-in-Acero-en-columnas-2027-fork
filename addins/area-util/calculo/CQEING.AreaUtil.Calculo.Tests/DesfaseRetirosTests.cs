namespace CQEING.AreaUtil.Calculo.Tests;

public class DesfaseRetirosTests
{
    private static readonly Retiros Ficha = new(1.5, 2, 1, 1);

    private static Desfase Rectangulo(Poligono lote, int frente = 0) =>
        DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, frente), Ficha);

    [Fact]
    public void El_lote_de_7_por_14_deja_52_5_m2_utiles()
    {
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14));
        Assert.Null(d.Problema);
        Assert.Equal(52.5, d.Util!.Area, 6);
        Assert.Contains(new Punto(1, 1.5), d.Util.Vertices);
        Assert.Contains(new Punto(6, 12), d.Util.Vertices);
    }

    [Fact]
    public void En_sentido_horario_el_area_util_es_la_misma()
    {
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        Desfase d = Rectangulo(horario, 3);
        Assert.Equal(52.5, d.Util!.Area, 6);
    }

    [Fact]
    public void Con_el_frente_en_otro_lado_cambian_los_retiros()
    {
        // Frente a la derecha (lado 1, x = 7): ese lado se corre 1.5 y el de la izquierda (posterior) 2.
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14), 1);
        Assert.Equal((7 - 1.5 - 2) * (14 - 1 - 1), d.Util!.Area, 6);
    }

    [Fact]
    public void Retiros_en_cero_dejan_el_mismo_lote() =>
        Assert.Equal(98, DesfaseRetiros.Calcular(Poligono.Rectangulo(7, 14), LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 0), new Retiros(0, 0, 0, 0)).Util!.Area, 6);

    [Fact]
    public void Un_trapecio_tambien_se_desfasa()
    {
        var trapecio = new Poligono([new(0, 0), new(10, 0), new(8, 12), new(2, 12)]);
        Desfase d = Rectangulo(trapecio);
        Assert.Null(d.Problema);
        Assert.True(d.Util!.Area > 0 && d.Util.Area < trapecio.Area);
        Assert.All(d.Util.Vertices, v => Assert.True(trapecio.Contiene(v)));
    }

    [Fact]
    public void Un_lote_angosto_con_laterales_que_se_comen_el_frente_da_problema()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Desfase d = DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(1.5, 2, 4, 4));
        Assert.Null(d.Util);
        Assert.Contains("se comen", d.Problema);
    }

    [Fact]
    public void Un_retiro_negativo_da_problema()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Assert.NotNull(DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(-1, 2, 1, 1)).Problema);
    }

    [Fact]
    public void Los_trazos_no_se_superponen_y_el_area_util_queda_cerrada()
    {
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.Lote));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.AreaUtil));
        // Cada línea de retiro va del lado vecino a la esquina del área útil: dos tramos por retiro.
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroFrontal));
        Assert.Contains(d.Trazos, t => t.Estilo == Estilos.RetiroFrontal && t.A == new Punto(0, 1.5) && t.B == new Punto(1, 1.5));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.RetiroLateral));
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroPosterior));
    }

    [Fact]
    public void Un_retiro_en_cero_no_dibuja_tramos_de_ese_lado()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Desfase d = DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(1.5, 2, 0, 1));
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroLateral));
    }
}
