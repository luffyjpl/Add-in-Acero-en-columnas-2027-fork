namespace CQEING.Despiece.Calculo;

/// <summary>El nombre de una columna, como lo muestra Revit en la cuadrícula (decisión 15 del plan de columnas).</summary>
public static class NombreColumna
{
    /// <summary>
    /// La marca de ubicación de Revit tal cual (<c>1-6</c>, <c>C-2</c>), también con la distancia al eje que agrega entre
    /// paréntesis cuando la columna no está justo en él (<c>C(-0.15)-2</c>). Solo sin espacios en los extremos.
    /// </summary>
    public static string DeMarca(string marca) => marca.Trim();
}
