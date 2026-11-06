namespace CQEING.Despiece.Calculo;

/// <summary>
/// Una zona de estribos seguidos con la misma separación, en metros. <paramref name="Indices"/>: de qué alturas de la
/// entrada salió, para saber a qué barra de Revit pertenece. Con un solo estribo la separación es 0.
/// </summary>
public sealed record ZonaEstribos(int Cantidad, double Separacion, double Desde, double Hasta, IReadOnlyList<int> Indices)
{
    public double Centro => (Desde + Hasta) / 2;
}

/// <summary>
/// Junta estribos en zonas para etiquetarlos <c>15 #3 @0.14</c> (#227, hallazgo 29 del plan de columnas): hay modelos
/// donde cada estribo es un elemento suelto, y una etiqueta por estribo llena la vista de <c>1 #3</c>.
/// </summary>
public static class ZonasEstribos
{
    /// <summary>
    /// Ordena las alturas; las que están a menos de <paramref name="tolerancia"/> (estribo y grapa) cuentan una vez; los
    /// seguidos con la misma separación (con esa tolerancia) forman una zona, y una separación mayor que
    /// <paramref name="maximo"/> la corta.
    /// </summary>
    public static IReadOnlyList<ZonaEstribos> Agrupar(IReadOnlyList<double> alturas, double tolerancia = 0.01, double maximo = 0.40)
    {
        var niveles = new List<(double Z, List<int> Indices)>();
        foreach (int i in Enumerable.Range(0, alturas.Count).OrderBy(i => alturas[i]))
        {
            if (niveles.Count > 0 && alturas[i] - niveles[^1].Z < tolerancia)
                niveles[^1].Indices.Add(i);
            else
                niveles.Add((alturas[i], [i]));
        }

        var zonas = new List<ZonaEstribos>();
        var actual = new List<(double Z, List<int> Indices)>();
        double separacion = 0;
        void Cerrar()
        {
            if (actual.Count == 0)
                return;
            double desde = actual[0].Z, hasta = actual[^1].Z;
            zonas.Add(new ZonaEstribos(actual.Count, actual.Count > 1 ? (hasta - desde) / (actual.Count - 1) : 0, desde, hasta,
                actual.SelectMany(n => n.Indices).ToList()));
            actual = [];
        }
        for (int n = 0; n < niveles.Count; n++)
        {
            double hueco = actual.Count > 0 ? niveles[n].Z - actual[^1].Z : 0;
            bool sigue = actual.Count == 1 ? hueco <= maximo && !EmpiezaOtra(n, hueco) : actual.Count > 1 && Math.Abs(hueco - separacion) <= tolerancia;
            if (!sigue)
                Cerrar();
            else if (actual.Count == 1)
                separacion = hueco;
            actual.Add(niveles[n]);
        }
        Cerrar();
        return zonas;

        // Un estribo solo no se empareja con el siguiente si ese no sigue la separación del par pero sí arranca una zona
        // pareja con el de después: el suelto que deja el reparto de columnas al igualar los dos últimos espacios de la
        // zona central se escribe «1 #3», y el confinado de arriba queda entero (decisión 10 del plan
        // 2026-11-05-largos-y-estribos-columnas-245-256).
        bool EmpiezaOtra(int n, double hueco)
        {
            if (n + 2 >= niveles.Count)
                return false;
            double siguiente = niveles[n + 1].Z - niveles[n].Z, despues = niveles[n + 2].Z - niveles[n + 1].Z;
            return Math.Abs(siguiente - hueco) > tolerancia && Math.Abs(despues - siguiente) <= tolerancia;
        }
    }
}
