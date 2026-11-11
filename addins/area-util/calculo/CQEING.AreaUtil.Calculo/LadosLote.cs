namespace CQEING.AreaUtil.Calculo;

/// <summary>El papel de cada lado del lote frente a la norma (min 3–5 y 33 de la reunión del 09-nov).</summary>
public enum TipoLado
{
    Frente,
    Posterior,
    LateralIzquierdo,
    LateralDerecho,
}

/// <summary>Los retiros de la ficha de la curaduría, en metros.</summary>
public sealed record Retiros(double Frontal, double Posterior, double LateralIzquierdo, double LateralDerecho)
{
    public double Para(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => Frontal,
        TipoLado.Posterior => Posterior,
        TipoLado.LateralIzquierdo => LateralIzquierdo,
        _ => LateralDerecho,
    };
}

/// <summary>
/// Decisión 19 del plan: el usuario marca el frente y cada otro lado se clasifica por hacia dónde mira; luego se
/// puede cambiar a mano. En un lote en L el escalón y el fondo real miran los dos hacia atrás: los dos son posteriores.
/// </summary>
public static class LadosLote
{
    /// <summary>Un lado que mira más de 45° hacia el fondo (o hacia la calle) deja de ser lateral.</summary>
    private static readonly double Coseno45 = Math.Sqrt(0.5);

    public static TipoLado[] Clasificar(Poligono lote, int frente)
    {
        int n = lote.Lados;
        frente = ((frente % n) + n) % n;
        Punto haciaElFondo = lote.NormalAdentro(frente);
        // Quien está en la calle mirando hacia el lote tiene la derecha girando 90° en sentido horario.
        var derecha = new Punto(haciaElFondo.Y, -haciaElFondo.X);

        var tipos = new TipoLado[n];
        for (int i = 0; i < n; i++)
        {
            Punto afuera = -lote.NormalAdentro(i);
            double haciaAtras = afuera.Escalar(haciaElFondo);
            tipos[i] = i == frente || haciaAtras < -Coseno45 ? TipoLado.Frente
                : haciaAtras > Coseno45 ? TipoLado.Posterior
                : afuera.Escalar(derecha) > 0 ? TipoLado.LateralDerecho
                : TipoLado.LateralIzquierdo;
        }
        return tipos;
    }
}
