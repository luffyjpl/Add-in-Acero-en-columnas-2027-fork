using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Vigas.Calculo.Seccion;

/// <summary>
/// Reparte la piel (acero lateral) en el corte (06-nov): pegada al estribo por dentro y a la misma distancia entre la
/// última fila de abajo y la primera de arriba. Solo geometría; el gancho y los ids los pone quien la use.
/// </summary>
public static class RepartidorPiel
{
    /// <param name="piel">Cantidad = barras por lado; Diametro = número de la barra.</param>
    /// <param name="altoMinimo">
    /// Alto del tramo más bajo de la viga: la fila de arriba baja lo que la sección sobra de ese alto, conservando su
    /// distancia a la cara de arriba, para que la piel entre en todos los tramos.
    /// </param>
    public static UbicacionPiel Ubicar(EntradaSeccion e, ResultadoSeccion s, GrupoBarras piel, double altoMinimo)
    {
        if (s.Superior.Error != ErrorCara.Ninguno || s.Inferior.Error != ErrorCara.Ninguno
            || s.Superior.Filas.Count == 0 || s.Inferior.Filas.Count == 0)
            return new UbicacionPiel([], 0);

        double yAbajo = s.Inferior.Filas.SelectMany(f => f).Max(b => b.Y);
        double yArriba = s.Superior.Filas.SelectMany(f => f).Min(b => b.Y) - (e.Alto - altoMinimo);
        double hueco = yArriba - yAbajo;
        double diametro = e.Reglas.PorDiametro[piel.Diametro].Diametro;
        double paso = diametro + e.Reglas.DistanciaLibre;

        int caben = 0;
        while (hueco / (caben + 2) >= paso) caben++;

        double x = e.Recubrimiento.Lados + e.Reglas.PorDiametro[e.DiametroEstribo].Diametro + diametro / 2;
        int cuantas = Math.Min(piel.Cantidad, caben);
        var alturas = new List<PielEnSeccion>(cuantas);
        for (int k = 1; k <= cuantas; k++)
        {
            double y = yAbajo + k * hueco / (cuantas + 1);
            alturas.Add(new PielEnSeccion(k, new BarraEnSeccion(piel.Diametro, x, y),
                new BarraEnSeccion(piel.Diametro, e.Ancho - x, y)));
        }
        return new UbicacionPiel(alturas, caben);
    }

    /// <summary>Las barras de la mitad de arriba rematan su punta con gancho hacia arriba; la del medio no.</summary>
    public static bool GanchoArriba(int altura, int alturas) => 2 * altura > alturas + 1;
}
