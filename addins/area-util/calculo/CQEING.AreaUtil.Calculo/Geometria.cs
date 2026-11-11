namespace CQEING.AreaUtil.Calculo;

/// <summary>Un punto o un vector en planta, en metros.</summary>
public readonly record struct Punto(double X, double Y)
{
    public static Punto operator +(Punto a, Punto b) => new(a.X + b.X, a.Y + b.Y);
    public static Punto operator -(Punto a, Punto b) => new(a.X - b.X, a.Y - b.Y);
    public static Punto operator -(Punto a) => new(-a.X, -a.Y);
    public static Punto operator *(Punto a, double k) => new(a.X * k, a.Y * k);

    public double Cruz(Punto b) => X * b.Y - Y * b.X;
    public double Escalar(Punto b) => X * b.X + Y * b.Y;
    public double Largo => Math.Sqrt(X * X + Y * Y);

    // Se divide (no se multiplica por 1/largo) para que (7, 0) dé exactamente (1, 0).
    public Punto Unitario() => new(X / Largo, Y / Largo);
}

/// <summary>Rectas infinitas dadas por un punto y una dirección.</summary>
public static class Rectas
{
    /// <summary>Corte de <c>p + t·u</c> con <c>q + s·v</c>. False si son paralelas.</summary>
    public static bool Interseccion(Punto p, Punto u, Punto q, Punto v, out Punto corte)
    {
        double denominador = u.Cruz(v);
        if (Math.Abs(denominador) < 1e-12)
        {
            corte = default;
            return false;
        }
        corte = p + u * ((q - p).Cruz(v) / denominador);
        return true;
    }
}

/// <summary>
/// El contorno de un lote o del área útil, en metros. Quita los puntos repetidos (también el último igual al primero,
/// como lo cierran los topógrafos) y los que quedan alineados entre sus vecinos.
/// </summary>
public sealed class Poligono
{
    /// <summary>Un milímetro: por debajo, dos puntos son el mismo.</summary>
    public const double Tolerancia = 1e-3;

    public Poligono(IEnumerable<Punto> puntos)
    {
        var limpios = new List<Punto>();
        foreach (Punto p in puntos)
            if (limpios.Count == 0 || (p - limpios[^1]).Largo > Tolerancia)
                limpios.Add(p);
        if (limpios.Count > 1 && (limpios[0] - limpios[^1]).Largo <= Tolerancia)
            limpios.RemoveAt(limpios.Count - 1);
        bool quito = true;
        while (quito && limpios.Count > 3)
        {
            quito = false;
            for (int i = 0; i < limpios.Count; i++)
            {
                Punto a = limpios[(i - 1 + limpios.Count) % limpios.Count], b = limpios[i], c = limpios[(i + 1) % limpios.Count];
                Punto ab = b - a, bc = c - b;
                // Alineado y siguiendo el mismo sentido: el punto del medio no cambia la forma.
                if (Math.Abs(ab.Unitario().Cruz(bc.Unitario())) < 1e-9 && ab.Escalar(bc) > 0)
                {
                    limpios.RemoveAt(i);
                    quito = true;
                    break;
                }
            }
        }
        Vertices = limpios;
    }

    public IReadOnlyList<Punto> Vertices { get; }

    public int Lados => Vertices.Count;

    public static Poligono Rectangulo(double frente, double fondo) => new([new(0, 0), new(frente, 0), new(frente, fondo), new(0, fondo)]);

    public (Punto A, Punto B) Lado(int i) => (Vertices[i], Vertices[(i + 1) % Lados]);

    public double LargoLado(int i) => (Lado(i).B - Lado(i).A).Largo;

    /// <summary>Positiva en sentido antihorario. Se mide desde el primer punto para no perder precisión con coordenadas grandes.</summary>
    public double AreaConSigno
    {
        get
        {
            if (Lados < 3)
                return 0;
            Punto o = Vertices[0];
            double doble = 0;
            for (int i = 1; i < Lados - 1; i++)
                doble += (Vertices[i] - o).Cruz(Vertices[i + 1] - o);
            return doble / 2;
        }
    }

    public double Area => Math.Abs(AreaConSigno);

    public bool EsAntihorario => AreaConSigno > 0;

    /// <summary>La normal unitaria del lado <paramref name="i"/> que apunta hacia adentro del polígono.</summary>
    public Punto NormalAdentro(int i)
    {
        (Punto a, Punto b) = Lado(i);
        Punto d = (b - a).Unitario();
        var izquierda = new Punto(-d.Y, d.X);
        return EsAntihorario ? izquierda : -izquierda;
    }

    /// <summary>True si dos lados que no son vecinos se tocan.</summary>
    public bool SeCruza()
    {
        for (int i = 0; i < Lados; i++)
            for (int j = i + 1; j < Lados; j++)
            {
                bool vecinos = j == i + 1 || (i == 0 && j == Lados - 1);
                if (!vecinos && SeTocan(Lado(i), Lado(j)))
                    return true;
            }
        return false;
    }

    /// <summary>Regla par-impar: un rayo hacia la derecha cruza un número impar de lados si el punto está adentro.</summary>
    public bool Contiene(Punto p)
    {
        bool adentro = false;
        for (int i = 0, j = Lados - 1; i < Lados; j = i++)
        {
            Punto a = Vertices[i], b = Vertices[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                adentro = !adentro;
        }
        return adentro;
    }

    /// <summary>Adentro, o a menos de <see cref="Tolerancia"/> de un lado: con un retiro en cero la esquina cae en el borde.</summary>
    public bool ContieneOBorde(Punto p) => Contiene(p) || Enumerable.Range(0, Lados).Any(i => DistanciaAlLado(i, p) <= Tolerancia);

    private double DistanciaAlLado(int i, Punto p)
    {
        (Punto a, Punto b) = Lado(i);
        Punto ab = b - a;
        double t = Math.Clamp((p - a).Escalar(ab) / ab.Escalar(ab), 0, 1);
        return (p - (a + ab * t)).Largo;
    }

    private static bool SeTocan((Punto A, Punto B) s, (Punto A, Punto B) t)
    {
        double o1 = (s.B - s.A).Cruz(t.A - s.A), o2 = (s.B - s.A).Cruz(t.B - s.A);
        double o3 = (t.B - t.A).Cruz(s.A - t.A), o4 = (t.B - t.A).Cruz(s.B - t.A);
        if (((o1 > 0 && o2 < 0) || (o1 < 0 && o2 > 0)) && ((o3 > 0 && o4 < 0) || (o3 < 0 && o4 > 0)))
            return true;
        return (Math.Abs(o1) < 1e-12 && EnSegmento(s, t.A)) || (Math.Abs(o2) < 1e-12 && EnSegmento(s, t.B))
            || (Math.Abs(o3) < 1e-12 && EnSegmento(t, s.A)) || (Math.Abs(o4) < 1e-12 && EnSegmento(t, s.B));
    }

    private static bool EnSegmento((Punto A, Punto B) s, Punto p) =>
        p.X >= Math.Min(s.A.X, s.B.X) - 1e-12 && p.X <= Math.Max(s.A.X, s.B.X) + 1e-12
        && p.Y >= Math.Min(s.A.Y, s.B.Y) - 1e-12 && p.Y <= Math.Max(s.A.Y, s.B.Y) + 1e-12;
}
