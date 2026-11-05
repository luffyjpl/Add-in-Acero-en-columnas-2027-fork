using System.Globalization;
using CQEING.Vigas.Calculo.Generacion;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo;

/// <summary>Un choque para el tablero: el punto en los ejes de la zapata, las dos barras (ids de <see cref="Trazo.Pieza"/>) y el texto.</summary>
public sealed record ChoqueZapata(Punto3 Punto, string A, string B, string Texto, double SeMeteMm);

/// <summary>
/// Las barras de la zapata como trazos 3D por su eje, en mm y en los ejes de la zapata (z sobre la cara inferior), con los
/// dobleces partidos en tramos rectos: la barra que crea Revit, medida en H13 del plan de 2026-11-05. De aquí salen la
/// vista previa del tablero y los choques (V1 y V2 del plan), como <c>PreviaAcero</c> en vigas.
/// </summary>
public static class TrazosZapata
{
    /// <summary>Grados de arco por tramo recto al partir un doblez, como en vigas.</summary>
    private const double PasoArco = 15;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="doblados">Diámetro de doblado por número de barra, del tipo de barra del proyecto; sin él, el del cuadro.</param>
    public static IReadOnlyList<Trazo> Trazos(IReadOnlyList<ConjuntoBarras> conjuntos, Reglas reglas, IReadOnlyDictionary<int, double>? doblados)
    {
        var trazos = new List<Trazo>();
        for (int k = 0; k < conjuntos.Count; k++)
        {
            ConjuntoBarras c = conjuntos[k];
            List<(double A, double Z)> perfil = Perfil(c, reglas, doblados);
            for (int i = 0; i < c.Posiciones.Count; i++)
            {
                double t = c.Posiciones[i];
                List<Punto3> puntos = perfil.Select(p => c.Direccion == Direccion.X ? new Punto3(p.A, t, p.Z) : new Punto3(t, p.A, p.Z)).ToList();
                trazos.Add(new Trazo(puntos, c.Numero, c.Diametro, Pieza(k, i), TipoTrazo.Barra));
            }
        }
        return trazos;
    }

    /// <summary>El id de una barra: el índice de su conjunto en el resultado y el de su posición, «3.0».</summary>
    public static string Pieza(int conjunto, int barra) => $"{conjunto}.{barra}";

    /// <summary>Dónde se meten las barras una en la otra (decisión 31): el detector de vigas sobre estos mismos trazos.</summary>
    public static IReadOnlyList<ChoqueZapata> Choques(IReadOnlyList<ConjuntoBarras> conjuntos, IReadOnlyList<Trazo> trazos) =>
        DetectorChoques.Detectar(trazos)
            .Select(c => new ChoqueZapata(c.Punto, c.A.Pieza, c.B.Pieza,
                $"{Nombre(conjuntos, c.A.Pieza, true)} (#{c.A.Numero}) se mete {c.SeMeteMm.ToString("0.0", Inv)} mm en {Nombre(conjuntos, c.B.Pieza, false)} (#{c.B.Numero})",
                Math.Round(c.SeMeteMm, 1)))
            .ToList();

    /// <summary>«La barra 3 de la parrilla inferior en X»; se cuenta desde 1.</summary>
    private static string Nombre(IReadOnlyList<ConjuntoBarras> conjuntos, string pieza, bool alInicio)
    {
        string[] partes = pieza.Split('.');
        ConjuntoBarras c = conjuntos[int.Parse(partes[0], Inv)];
        int antes = conjuntos.Take(int.Parse(partes[0], Inv)).Where(o => o.Parrilla == c.Parrilla && o.Direccion == c.Direccion).Sum(o => o.Cantidad);
        string parrilla = c.Parrilla == Parrilla.Inferior ? "inferior" : "superior";
        return $"{(alInicio ? "La" : "la")} barra {antes + int.Parse(partes[1], Inv) + 1} de la parrilla {parrilla} en {c.Direccion}";
    }

    /// <summary>
    /// La barra en su plano: a lo largo de la dirección y en altura. Las curvas que recibe Revit terminan en la cara exterior
    /// del doblez, así que el tramo recto empieza <c>D/2 + d</c> hacia adentro, y el doblez es un arco de radio
    /// <c>D/2 + d/2</c> (H13). La pata de 90° llega a su punta; la L la lleva solo al inicio (decisión 37).
    /// </summary>
    private static List<(double A, double Z)> Perfil(ConjuntoBarras c, Reglas reglas, IReadOnlyDictionary<int, double>? doblados)
    {
        ReglaDiametro regla = reglas.PorDiametro[c.Numero];
        double d = c.Diametro;
        double doblado = doblados?.GetValueOrDefault(c.Numero) is > 0 and double delTipo ? delTipo : Reglas.DobladoGancho(regla, TipoGancho.De90);
        double radio = doblado / 2 + d / 2;
        // Hacia dónde dobla: arriba en la parrilla inferior, abajo en la superior.
        double s = c.Parrilla == Parrilla.Inferior ? 1 : -1;
        (int grados, double cola, bool alInicio, bool alFinal) = c.Terminacion switch
        {
            Terminacion.Pata90 => (90, Math.Max(0, c.Pata - d / 2 - radio), c.Patas != Patas.Ninguna, c.Patas == Patas.Ambas),
            Terminacion.Gancho135 => (135, c.Cola ?? regla.Estribo135.E ?? regla.Estribo135.B, true, true),
            Terminacion.Gancho180 => (180, c.Cola ?? regla.Gancho180.E ?? regla.Gancho180.B, true, true),
            _ => (0, 0, false, false),
        };
        double desde = alInicio ? c.Desde + doblado / 2 + d : c.Desde;
        double hasta = alFinal ? c.Hasta - doblado / 2 - d : c.Hasta;
        var perfil = new List<(double A, double Z)>();
        if (alInicio)
            perfil.AddRange(Gancho(desde, -1).AsEnumerable().Reverse());
        perfil.Add((desde, c.Z));
        perfil.Add((hasta, c.Z));
        if (alFinal)
            perfil.AddRange(Gancho(hasta, 1));
        return perfil;

        // Del final del tramo recto a la punta de la cola. afuera: −1 en el inicio, +1 en el final. Como en vigas.
        List<(double A, double Z)> Gancho(double punta, double afuera)
        {
            int n = Math.Max(1, (int)Math.Ceiling(grados / PasoArco));
            var gancho = new List<(double A, double Z)>();
            for (int i = 1; i <= n; i++)
            {
                double phi = grados * Math.PI / 180 * i / n;
                gancho.Add((punta + afuera * radio * Math.Sin(phi), c.Z + s * radio * (1 - Math.Cos(phi))));
            }
            double fin = grados * Math.PI / 180;
            (double A, double Z) ultimo = gancho[^1];
            gancho.Add((ultimo.A + afuera * Math.Cos(fin) * cola, ultimo.Z + s * Math.Sin(fin) * cola));
            return gancho;
        }
    }
}
