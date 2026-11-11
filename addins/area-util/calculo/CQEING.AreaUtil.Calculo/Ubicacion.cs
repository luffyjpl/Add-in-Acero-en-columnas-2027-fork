namespace CQEING.AreaUtil.Calculo;

/// <summary>
/// Dónde va el lote de frente × fondo en Revit (decisión 18 del plan): la esquina izquierda del frente en (X, Y), en
/// metros y coordenadas internas, y el giro del frente en grados, antihorario desde el eje X.
/// </summary>
public sealed record Ubicacion(double X, double Y, double GiroGrados)
{
    public static Ubicacion Origen { get; } = new(0, 0, 0);

    /// <summary>Gira <paramref name="p"/> alrededor del origen y lo lleva a la esquina.</summary>
    public Punto Aplicar(Punto p)
    {
        double a = GiroGrados * Math.PI / 180;
        double cos = Math.Cos(a), sen = Math.Sin(a);
        return new Punto(X + p.X * cos - p.Y * sen, Y + p.X * sen + p.Y * cos);
    }

    /// <summary>
    /// «Elegir en Revit»: el primer clic es la esquina y el segundo dice hacia dónde va el frente. Si los dos clics
    /// caen en el mismo punto, el frente queda sobre el eje X.
    /// </summary>
    public static Ubicacion DesdeDosPuntos(Punto esquina, Punto haciaElFrente)
    {
        Punto d = haciaElFrente - esquina;
        if (d.Largo <= Poligono.Tolerancia)
            return new Ubicacion(esquina.X, esquina.Y, 0);
        double giro = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        return new Ubicacion(esquina.X, esquina.Y, giro < 0 ? giro + 360 : giro);
    }
}
