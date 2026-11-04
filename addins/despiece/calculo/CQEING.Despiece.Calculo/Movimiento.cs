namespace CQEING.Despiece.Calculo;

/// <summary>
/// Lo que el usuario movió de una pieza en el paso 2, en metros. <paramref name="Dz"/>: la barra, hacia arriba, con su
/// texto y su gancho. <paramref name="Tx"/> y <paramref name="Tz"/>: el texto, además de eso. <paramref name="Dx"/>: la
/// barra hacia la derecha, en la columna, donde las barras sacadas se acomodan a los lados (decisión 14 del plan de
/// columnas); en la viga siempre es 0.
/// </summary>
public sealed record Movimiento(double Dz, double Tx, double Tz, double Dx = 0);
