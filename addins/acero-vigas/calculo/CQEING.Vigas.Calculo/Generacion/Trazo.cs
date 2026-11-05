namespace CQEING.Vigas.Calculo.Generacion;

// Aparte de PreviaAcero y MarcoPortico para que zapatas enlace DetectorChoques sin arrastrar el resto de vigas
// (decisión 31 del plan docs/planes/2026-11-05-correcciones-zapatas-reunion.md).

/// <summary>Un punto del modelo, en mm.</summary>
public readonly record struct Punto3(double X, double Y, double Z);

/// <summary>De qué es un <see cref="Trazo"/>: la grapa es el estribo abierto.</summary>
public enum TipoTrazo
{
    Barra,
    Baston,
    Estribo,
    Grapa,
}

/// <summary>
/// Una barra de la vista previa 3D: su eje, en mm del modelo, con los dobleces ya partidos en tramos rectos.
/// <paramref name="Numero"/> es el diámetro en octavos (para el color) y <paramref name="Diametro"/>, el real en mm.
/// <paramref name="Pieza"/> es el id del tablero (<c>S1</c>, <c>BS1</c>, <c>E</c>, <c>E1</c>) y <paramref name="Tramo"/>,
/// el del estribo (null en las piezas longitudinales): con ellos se dice qué toca a qué en los choques.
/// </summary>
public sealed record Trazo(IReadOnlyList<Punto3> Puntos, int Numero, double Diametro, string Pieza, TipoTrazo Tipo, string? Tramo = null);
