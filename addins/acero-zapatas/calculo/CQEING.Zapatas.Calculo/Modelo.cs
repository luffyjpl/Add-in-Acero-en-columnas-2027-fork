namespace CQEING.Zapatas.Calculo;

/// <summary>Un punto en planta, en mm.</summary>
public readonly record struct Punto(double X, double Y);

/// <summary>Las dos direcciones del acero de una zapata: siempre a 90°, como un cuaderno cuadriculado.</summary>
public enum Direccion { X, Y }

public enum Parrilla { Inferior, Superior }

/// <summary>
/// Cómo termina una barra: recta, con pata ajustada a la altura (90°) o con gancho del cuadro (135° y 180°).
/// <see cref="Cerrada"/>: la barra da la vuelta completa en el corte (abajo, los costados y arriba) y cierra con dos
/// ganchos de 135° de estribo en la esquina de arriba del inicio (decisión 43 del plan de 2026-11-05). Solo en la parrilla
/// inferior: reemplaza también a la superior de esa dirección.
/// </summary>
public enum Terminacion { Recta, Pata90, Gancho135, Gancho180, Cerrada }

/// <summary>
/// Dónde lleva pata una barra de 90°, según la forma elegida (decisión 37 del plan de 2026-11-05): en U en las dos puntas, en
/// L solo al inicio (la punta de menor X o de menor Y, como la 17A) y recta en ninguna.
/// </summary>
public enum Patas { Ambas, Inicio, Ninguna }

/// <summary>
/// Hasta dónde llega la pata de 90° (decisión 29): el largo B del cuadro, como vigas y columnas, o hasta el recubrimiento
/// opuesto, como en la <c>v0.1.0</c>. Si el del cuadro no cabe, se corta en el recubrimiento.
/// </summary>
public enum LargoPata { Cuadro, Recubrimiento }

/// <summary>Los recubrimientos de la zapata, en mm: abajo, a los lados y arriba (decisión 8 del plan de #247).</summary>
public sealed record Recubrimientos(double Inferior, double Lateral, double Superior)
{
    public static Recubrimientos Global(double mm) => new(mm, mm, mm);
}

/// <summary>
/// Cómo terminan las barras de una parrilla en una dirección. <paramref name="Cola"/>: el tramo recto del gancho de 135° o
/// 180° que pone Revit con el tipo de gancho elegido, en mm (H13 del plan de 2026-11-05); null si no se conoce. Solo lo usa
/// el dibujo: el resumen suma el largo del cuadro.
/// </summary>
public sealed record Extremos(Terminacion Terminacion = Terminacion.Pata90, Patas Patas = Patas.Ambas, double? Cola = null);

/// <summary>
/// El acero de una dirección: número de barra, separación máxima en mm y cómo terminan las barras de la parrilla inferior.
/// <paramref name="Superior"/>: cómo terminan las de la superior, que elige su gancho aparte (decisión 30); null, igual
/// que la inferior.
/// </summary>
public sealed record ConfigDireccion(int Numero, double Separacion, Terminacion Terminacion = Terminacion.Pata90, Patas Patas = Patas.Ambas,
    double? Cola = null, Extremos? Superior = null)
{
    public Extremos En(Parrilla p) => p == Parrilla.Superior && Superior is not null ? Superior : new(Terminacion, Patas, Cola);
}

/// <summary>
/// Lo que elige el modelador. La parrilla superior repite el número y la separación (Carlos, reunión del 04-nov).
/// <paramref name="Doblados"/>: diámetro de doblado por número de barra, en mm, del tipo de barra del proyecto; sin él, el del
/// cuadro. Revit dobla con el del tipo, que puede ser mayor (decisión 26 del plan de #246).
/// </summary>
/// <paramref name="DobladosEstribo"/>: lo mismo para los dobleces de estribo, que usa la barra cerrada (decisión 43; H21).
public sealed record ConfigParrillas(ConfigDireccion X, ConfigDireccion Y, Recubrimientos Recubrimientos, bool IncluirSuperior = true,
    IReadOnlyDictionary<int, double>? Doblados = null, LargoPata LargoPata = LargoPata.Cuadro, IReadOnlyDictionary<int, double>? DobladosEstribo = null)
{
    /// <summary>Si alguna dirección lleva la barra cerrada.</summary>
    public bool HayCerrada => X.Terminacion == Terminacion.Cerrada || Y.Terminacion == Terminacion.Cerrada;
}

/// <summary>Una zapata en sus propios ejes: contorno en mm, altura y centros de las columnas que se apoyan.</summary>
public sealed record ZapataEntrada(IReadOnlyList<Punto> Contorno, double Altura, IReadOnlyList<Punto> Columnas);

/// <summary>
/// Barras iguales y seguidas de una capa. Las líneas van de <see cref="Desde"/> a <see cref="Hasta"/> a lo largo de la
/// dirección, hasta la cara exterior del doblez, como las recibe Revit. <see cref="Posiciones"/> son las coordenadas
/// transversales; <see cref="Z"/> es la altura del eje de la barra sobre la cara inferior de la zapata.
/// <see cref="Pata"/> mide de la cara exterior de la barra a la punta; <see cref="Patas"/> dice en qué puntas va (con
/// <see cref="Terminacion.Pata90"/>). <see cref="Cola"/> es la del gancho de 135° o 180° (ver <see cref="Extremos"/>).
/// Con <see cref="Terminacion.Cerrada"/>, <see cref="Z"/> es el eje del tramo de abajo, <see cref="ZArriba"/> el del de
/// arriba y <see cref="Pata"/> la altura exterior de la barra; <see cref="Cola"/>, la del gancho de 135° de estribo.
/// </summary>
public sealed record ConjuntoBarras(Parrilla Parrilla, Direccion Direccion, int Capa, int Numero, double Diametro, double Z, double Desde, double Hasta,
    IReadOnlyList<double> Posiciones, Terminacion Terminacion, double Pata, double LargoNeto, double LongitudCorte, double Kilos,
    Patas Patas = Patas.Ambas, double? Cola = null, double? ZArriba = null)
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
