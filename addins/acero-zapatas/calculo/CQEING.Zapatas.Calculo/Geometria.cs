namespace CQEING.Zapatas.Calculo;

/// <summary>Geometría en planta de un contorno simple, en mm: ejes, recorte por el recubrimiento y cortes de las barras.</summary>
public static class Geometria
{
    private const double Tolerancia = 1e-6;

    public static double Area(IReadOnlyList<Punto> c)
    {
        double a = 0;
        for (int i = 0; i < c.Count; i++)
        {
            Punto p = c[i], q = c[(i + 1) % c.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>
    /// El ángulo de los ejes de la zapata, en radianes entre −45° y 45°: el del rectángulo más pequeño que envuelve el
    /// contorno, con X el lado más cercano a la X del proyecto (decisión 20).
    /// </summary>
    public static double AnguloEjes(IReadOnlyList<Punto> c)
    {
        double mejor = 0, menor = double.MaxValue;
        for (int i = 0; i < c.Count; i++)
        {
            Punto p = c[i], q = c[(i + 1) % c.Count];
            if (Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y) < Tolerancia)
                continue;
            double angulo = Normalizar(Math.Atan2(q.Y - p.Y, q.X - p.X));
            (double ancho, double alto) = Caja(Girar(c, -angulo));
            // Con áreas iguales gana el ángulo más cercano a la X del proyecto: un rectángulo sin girar da 0.
            double area = ancho * alto;
            if (area < menor * (1 - 1e-9) || (Math.Abs(area - menor) <= menor * 1e-9 && Math.Abs(angulo) < Math.Abs(mejor)))
                (mejor, menor) = (angulo, area);
        }
        return mejor;
    }

    private static double Normalizar(double angulo)
    {
        while (angulo > Math.PI / 4 + Tolerancia)
            angulo -= Math.PI / 2;
        while (angulo <= -Math.PI / 4 + Tolerancia)
            angulo += Math.PI / 2;
        return angulo;
    }

    public static List<Punto> Girar(IEnumerable<Punto> puntos, double angulo)
    {
        (double s, double co) = Math.SinCos(angulo);
        return puntos.Select(p => new Punto(p.X * co - p.Y * s, p.X * s + p.Y * co)).ToList();
    }

    /// <summary>Ancho (X) y alto (Y) de la caja que envuelve los puntos.</summary>
    public static (double Ancho, double Alto) Caja(IReadOnlyCollection<Punto> c) =>
        (c.Max(p => p.X) - c.Min(p => p.X), c.Max(p => p.Y) - c.Min(p => p.Y));

    /// <summary>Si el contorno es la caja que lo envuelve, dentro de 1 mm.</summary>
    public static bool EsRectangular(IReadOnlyList<Punto> c)
    {
        (double ancho, double alto) = Caja(c);
        return Math.Abs(Math.Abs(Area(c)) - ancho * alto) <= Math.Max(ancho, alto);
    }

    /// <summary>
    /// El contorno metido <paramref name="distancia"/> hacia adentro: cada lado se corre y las esquinas salen del cruce de
    /// lados vecinos, así que sirve para rectángulos, trapecios y formas en L. Vacía si la zapata no da para tanto.
    /// </summary>
    public static List<Punto> HaciaAdentro(IReadOnlyList<Punto> contorno, double distancia)
    {
        List<Punto> c = SinRepetidos(contorno);
        if (c.Count < 3)
            return [];
        double signo = Area(c) > 0 ? 1 : -1;
        var lados = new List<(Punto P, Punto D)>();
        for (int i = 0; i < c.Count; i++)
        {
            Punto p = c[i], q = c[(i + 1) % c.Count];
            double largo = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
            var d = new Punto((q.X - p.X) / largo, (q.Y - p.Y) / largo);
            var adentro = new Punto(-d.Y * signo, d.X * signo);
            lados.Add((new Punto(p.X + adentro.X * distancia, p.Y + adentro.Y * distancia), d));
        }
        var resultado = new List<Punto>();
        var direcciones = new List<Punto>();
        for (int i = 0; i < lados.Count; i++)
        {
            (Punto p, Punto d) = lados[i];
            (Punto q, Punto e) = lados[(i + 1) % lados.Count];
            double cruz = d.X * e.Y - d.Y * e.X;
            if (Math.Abs(cruz) < 1e-9)
                continue;
            double t = ((q.X - p.X) * e.Y - (q.Y - p.Y) * e.X) / cruz;
            resultado.Add(new Punto(p.X + d.X * t, p.Y + d.Y * t));
            direcciones.Add(e);
        }
        if (resultado.Count < 3)
            return [];
        // Si la zapata no da para tanto, algún lado corrido queda al revés: en un cuadrado, el área ni cambia de signo.
        for (int i = 0; i < resultado.Count; i++)
        {
            Punto a = resultado[i], b = resultado[(i + 1) % resultado.Count];
            if ((b.X - a.X) * direcciones[i].X + (b.Y - a.Y) * direcciones[i].Y <= Tolerancia)
                return [];
        }
        return resultado;
    }

    /// <summary>Los tramos del contorno cortados por una línea paralela a X a la altura <paramref name="y"/>, de menor a mayor.</summary>
    public static List<(double Desde, double Hasta)> Cortes(IReadOnlyList<Punto> c, double y)
    {
        var cruces = new List<double>();
        for (int i = 0; i < c.Count; i++)
        {
            Punto p = c[i], q = c[(i + 1) % c.Count];
            // Medio abierto: un vértice justo en la línea cuenta una sola vez.
            if ((p.Y <= y && q.Y > y) || (q.Y <= y && p.Y > y))
                cruces.Add(p.X + (y - p.Y) / (q.Y - p.Y) * (q.X - p.X));
        }
        cruces.Sort();
        var tramos = new List<(double, double)>();
        for (int i = 0; i + 1 < cruces.Count; i += 2)
            if (cruces[i + 1] - cruces[i] > Tolerancia)
                tramos.Add((cruces[i], cruces[i + 1]));
        return tramos;
    }

    private static List<Punto> SinRepetidos(IReadOnlyList<Punto> c)
    {
        var r = new List<Punto>();
        foreach (Punto p in c)
            if (r.Count == 0 || Math.Abs(r[^1].X - p.X) + Math.Abs(r[^1].Y - p.Y) > Tolerancia)
                r.Add(p);
        if (r.Count > 1 && Math.Abs(r[0].X - r[^1].X) + Math.Abs(r[0].Y - r[^1].Y) <= Tolerancia)
            r.RemoveAt(r.Count - 1);
        return r;
    }
}
