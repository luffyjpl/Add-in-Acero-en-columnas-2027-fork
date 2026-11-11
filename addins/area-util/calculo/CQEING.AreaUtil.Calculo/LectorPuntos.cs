using System.Globalization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Un punto de la topografía, en metros. La Z se guarda y se muestra; las líneas se dibujan en el nivel.</summary>
public sealed record PuntoLote(double X, double Y, double Z);

/// <summary>
/// Lee los puntos pegados desde Excel o AutoCAD: columnas X, Y y Z opcional (decisión 14 del plan). Acepta tabulador,
/// punto y coma, coma o espacios entre columnas, y coma o punto decimal.
/// </summary>
public static class LectorPuntos
{
    public static (List<PuntoLote> Puntos, List<string> Errores) Leer(string texto)
    {
        var puntos = new List<PuntoLote>();
        var errores = new List<string>();
        string[] filas = texto.Replace("\r", "").Split('\n');
        for (int i = 0; i < filas.Length; i++)
        {
            string fila = filas[i].Trim();
            if (fila.Length == 0)
                continue;
            List<string> campos = Campos(fila);
            // El rótulo del punto («P1») va adelante cuando hay 4 columnas o más.
            if (campos.Count >= 4 && !EsNumero(campos[0]))
                campos.RemoveAt(0);
            List<double> numeros = campos.Where(EsNumero).Select(Numero).ToList();
            if (numeros.Count == campos.Count && numeros.Count is 2 or 3)
                puntos.Add(new PuntoLote(numeros[0], numeros[1], numeros.Count == 3 ? numeros[2] : 0));
            else if (!(puntos.Count == 0 && errores.Count == 0 && numeros.Count == 0))
                errores.Add($"Fila {i + 1}: no se entiende «{fila}». Se esperan X, Y y Z opcional.");
        }
        return (puntos, errores);
    }

    private static List<string> Campos(string fila)
    {
        string[] partes = fila.Contains('\t') ? fila.Split('\t')
            : fila.Contains(';') ? fila.Split(';')
            : fila.Contains(',') && fila.Contains('.') ? fila.Split(',')
            : fila.Contains(',') && !fila.Contains(' ') ? fila.Split(',')
            : fila.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    private static bool EsNumero(string campo) => double.TryParse(Normalizar(campo), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static double Numero(string campo) => double.Parse(Normalizar(campo), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// Excel copia una celda con formato tal como se ve («1.200.000,50» o «1,200,000.50», decisión 17): si hay los dos
    /// separadores, el último es el decimal; si uno solo se repite, es de miles; si aparece una vez, es el decimal.
    /// </summary>
    private static string Normalizar(string campo)
    {
        int puntos = campo.Count(c => c == '.'), comas = campo.Count(c => c == ',');
        if (puntos > 0 && comas > 0)
        {
            char miles = campo.LastIndexOf('.') > campo.LastIndexOf(',') ? ',' : '.';
            return campo.Replace(miles.ToString(), "").Replace(',', '.');
        }
        if (comas > 1)
            return campo.Replace(",", "");
        if (puntos > 1)
            return campo.Replace(".", "");
        return campo.Replace(',', '.');
    }
}
