namespace CQEING.Zapatas.Calculo;

/// <summary>Un punto en planta, en mm.</summary>
public readonly record struct Punto(double X, double Y);

/// <summary>Las dos direcciones del acero de una zapata: siempre a 90°, como un cuaderno cuadriculado.</summary>
public enum Direccion { X, Y }

public enum Parrilla { Inferior, Superior }

/// <summary>Cómo termina una barra: recta, con pata ajustada a la altura (90°) o con gancho del cuadro (135° y 180°).</summary>
public enum Terminacion { Recta, Pata90, Gancho135, Gancho180 }

/// <summary>Los recubrimientos de la zapata, en mm: abajo, a los lados y arriba (decisión 8 del plan de #247).</summary>
public sealed record Recubrimientos(double Inferior, double Lateral, double Superior)
{
    public static Recubrimientos Global(double mm) => new(mm, mm, mm);
}

/// <summary>El acero de una dirección: número de barra, separación máxima en mm y terminación.</summary>
public sealed record ConfigDireccion(int Numero, double Separacion, Terminacion Terminacion = Terminacion.Pata90);

/// <summary>Lo que elige el modelador. La parrilla superior repite esta configuración (Carlos, reunión del 04-nov).</summary>
public sealed record ConfigParrillas(ConfigDireccion X, ConfigDireccion Y, Recubrimientos Recubrimientos, bool IncluirSuperior = true);

/// <summary>Una zapata en sus propios ejes: contorno en mm, altura y centros de las columnas que se apoyan.</summary>
public sealed record ZapataEntrada(IReadOnlyList<Punto> Contorno, double Altura, IReadOnlyList<Punto> Columnas);

/// <summary>
/// Barras iguales y seguidas de una capa. Las líneas van de <see cref="Desde"/> a <see cref="Hasta"/> a lo largo de la
/// dirección, hasta la cara exterior del doblez, como las recibe Revit. <see cref="Posiciones"/> son las coordenadas
/// transversales; <see cref="Z"/> es la altura del eje de la barra sobre la cara inferior de la zapata.
/// </summary>
public sealed record ConjuntoBarras(Parrilla Parrilla, Direccion Direccion, int Capa, int Numero, double Diametro, double Z, double Desde, double Hasta,
    IReadOnlyList<double> Posiciones, Terminacion Terminacion, double Pata, double LargoNeto, double LongitudCorte, double Kilos)
{
    public int Cantidad => Posiciones.Count;

    /// <summary>La separación real, en mm; 0 si es una sola barra.</summary>
    public double Separacion => Cantidad > 1 ? (Posiciones[^1] - Posiciones[0]) / (Cantidad - 1) : 0;
}

/// <summary>El total de la zapata. La longitud usa la longitud de corte redondeada, igual que el peso (decisión 18).</summary>
public sealed record ResumenAcero(int TotalBarras, double LongitudTotal, double Kilos);

/// <summary>Lo que calcula el motor. Con <see cref="Errores"/> no se puede generar; <see cref="Avisos"/> no bloquea.</summary>
public sealed record ResultadoZapata(IReadOnlyList<ConjuntoBarras> Conjuntos, ResumenAcero Resumen, Direccion Principal,
    IReadOnlyList<string> Avisos, IReadOnlyList<string> Errores);
