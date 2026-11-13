namespace CQEING.AreaUtil.Calculo;

/// <summary>Qué hizo un clic del dibujo libre.</summary>
public enum PasoTrazado
{
    Agregado,
    Repetido,
    Cerrado,
}

/// <summary>
/// El dibujo libre del lote sin Revit (decisión 21 del plan): cada clic agrega una esquina, un clic sobre la anterior
/// se ignora y, con 3 esquinas o más, un clic a menos de 10 cm de la primera cierra el lote. En metros.
/// </summary>
public sealed class TrazadoLote
{
    /// <summary>Un clic a menos de 10 cm del primer punto cierra el lote.</summary>
    public const double ToleranciaCierre = 0.10;

    private readonly List<Punto> puntos = [];

    public IReadOnlyList<Punto> Puntos => puntos;

    public bool Cerrado { get; private set; }

    public bool PuedeTerminar => puntos.Count >= 3;

    public PasoTrazado Agregar(Punto p)
    {
        if (Cerrado)
            return PasoTrazado.Cerrado;
        if (PuedeTerminar && (p - puntos[0]).Largo <= ToleranciaCierre)
        {
            Cerrado = true;
            return PasoTrazado.Cerrado;
        }
        if (puntos.Count > 0 && (p - puntos[^1]).Largo <= Poligono.Tolerancia)
            return PasoTrazado.Repetido;
        puntos.Add(p);
        return PasoTrazado.Agregado;
    }

    /// <summary>Quita el último punto; false si no había ninguno.</summary>
    public bool Deshacer()
    {
        if (puntos.Count == 0)
            return false;
        puntos.RemoveAt(puntos.Count - 1);
        Cerrado = false;
        return true;
    }
}
