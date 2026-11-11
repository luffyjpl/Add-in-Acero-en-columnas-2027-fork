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

/// <summary>Decisión 7 del spec: el usuario marca el frente y el resto se deduce; luego se puede cambiar a mano.</summary>
public static class LadosLote
{
    public static TipoLado[] Clasificar(Poligono lote, int frente)
    {
        int n = lote.Lados;
        frente = ((frente % n) + n) % n;
        (Punto fa, Punto fb) = lote.Lado(frente);
        Punto direccion = (fb - fa).Unitario();
        Punto adentro = lote.NormalAdentro(frente);
        // Quien está en la calle mirando hacia el lote tiene la derecha girando 90° en sentido horario.
        var derecha = new Punto(adentro.Y, -adentro.X);
        Punto medioFrente = (fa + fb) * 0.5;

        int posterior = -1;
        double masOpuesto = double.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (i == frente)
                continue;
            (Punto a, Punto b) = lote.Lado(i);
            double coseno = direccion.Escalar((b - a).Unitario());
            if (coseno < masOpuesto)
            {
                masOpuesto = coseno;
                posterior = i;
            }
        }

        var tipos = new TipoLado[n];
        for (int i = 0; i < n; i++)
        {
            (Punto a, Punto b) = lote.Lado(i);
            tipos[i] = i == frente ? TipoLado.Frente
                : i == posterior ? TipoLado.Posterior
                : ((a + b) * 0.5 - medioFrente).Escalar(derecha) > 0 ? TipoLado.LateralDerecho
                : TipoLado.LateralIzquierdo;
        }
        return tipos;
    }
}
