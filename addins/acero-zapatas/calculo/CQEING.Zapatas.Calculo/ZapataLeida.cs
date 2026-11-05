namespace CQEING.Zapatas.Calculo;

/// <summary>Individual: una columna o ninguna. Combinada: dos o más columnas sobre la misma zapata.</summary>
public enum TipoZapata { Individual, Combinada }

public enum FormaZapata { Cuadrada, Rectangular, Atipica }

/// <summary>Una columna que se apoya en la zapata: centro en los ejes de la zapata y sección, en mm.</summary>
public sealed record ColumnaLeida(string Id, Punto Centro, double Ancho, double Alto);

/// <summary>
/// Lo que se saca de una zapata del modelo (#248), en mm. <see cref="Contorno"/> va en los ejes de la zapata, que nacen en
/// el origen del modelo, giran <see cref="Angulo"/> radianes y tienen la altura cero en <see cref="ZBase"/>. Los
/// recubrimientos son null si el elemento no los trae.
/// </summary>
public sealed record ZapataLeida(string Id, string Nombre, string Eje, string Nivel, double Angulo, double ZBase, double Altura,
    IReadOnlyList<Punto> Contorno, IReadOnlyList<ColumnaLeida> Columnas, double? RecubrimientoInferior, double? RecubrimientoLateral, double? RecubrimientoSuperior)
{
    public TipoZapata Tipo => Columnas.Count >= 2 ? TipoZapata.Combinada : TipoZapata.Individual;

    public FormaZapata Forma
    {
        get
        {
            if (!Geometria.EsRectangular(Contorno))
                return FormaZapata.Atipica;
            (double ancho, double alto) = Geometria.Caja(Contorno);
            return Math.Abs(ancho - alto) <= 1 ? FormaZapata.Cuadrada : FormaZapata.Rectangular;
        }
    }

    public ZapataEntrada Entrada => new(Contorno, Altura, Columnas.Select(c => c.Centro).ToList());
}
