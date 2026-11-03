namespace practicacq.Modelo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Las decisiones de <c>LectorContexto</c> que no necesitan Revit: cuál
/// cimentación es la zapata, cómo se juntan las bandas de nudo y cómo se cose
/// la columna en tramos. Están acá para probarlas en tools/PruebasTraslapos
/// (plan docs/planes/2026-11-03-contra-zapata-piso-entero.md).
///
/// Las unidades son las de quien llama: <c>LectorContexto</c> trabaja en pies.
/// </summary>
public static class TramosColumna
{
    /// <summary>Un pedazo de la columna, todavía sin los datos del elemento de Revit.</summary>
    public readonly record struct SubTramo(double Inf, double Sup, TipoTramo Tipo);

    /// <summary>
    /// De las cimentaciones que tocan la columna en planta, la que es SU zapata:
    /// la que contiene el pie de la columna en altura (con
    /// <paramref name="tolerancia"/> para la columna apoyada a ras de la cara
    /// superior). Si hay varias, la más profunda. <c>null</c> si ninguna.
    ///
    /// Antes se tomaba la de cara superior más alta entre todas, y una viga de
    /// amarre modelada como cimentación ganaba siempre: el arranque subía a su
    /// cara superior y todo el primer piso quedaba como contra zapata.
    /// </summary>
    public static int? ElegirZapata(IReadOnlyList<(double Inf, double Sup)> cimentaciones, double zPie, double tolerancia)
    {
        int? elegida = null;

        for (int i = 0; i < cimentaciones.Count; i++)
        {
            (double inf, double sup) = cimentaciones[i];
            if (zPie < inf - tolerancia || zPie > sup + tolerancia) continue;
            if (elegida is null || inf < cimentaciones[elegida.Value].Inf) elegida = i;
        }

        return elegida;
    }

    /// <summary>
    /// Junta las bandas que se pisan o quedan pegadas (a menos de
    /// <paramref name="tramoMinimo"/>) y las devuelve ordenadas de abajo hacia
    /// arriba. En un nudo suelen concurrir dos o cuatro vigas al mismo canto, y
    /// eso es un solo nudo, no cuatro.
    /// </summary>
    public static List<(double Inf, double Sup)> Fusionar(IEnumerable<(double Inf, double Sup)> bandas, double tramoMinimo)
    {
        var fusionadas = new List<(double Inf, double Sup)>();

        foreach ((double inf, double sup) in bandas.OrderBy(b => b.Inf))
        {
            if (fusionadas.Count > 0 && inf <= fusionadas[^1].Sup + tramoMinimo)
            {
                (double antInf, double antSup) = fusionadas[^1];
                fusionadas[^1] = (antInf, Math.Max(antSup, sup));
            }
            else
            {
                fusionadas.Add((inf, sup));
            }
        }

        return fusionadas;
    }

    /// <summary>
    /// Cose los nudos de UN elemento con lo que queda libre entre ellos, de
    /// abajo hacia arriba y sin dejar huecos.
    ///
    /// Solo salen tramos libres y nudos, nunca contra zapata: los estribos no
    /// pueden ir DENTRO de la zapata, pero cerca sí (ingeniero Santiago,
    /// 3-nov). Lo de adentro ni siquiera llega acá, porque quien llama arranca
    /// en la cara superior de la zapata (<paramref name="zArranque"/>).
    /// </summary>
    public static List<SubTramo> Coser(
        double zArranque,
        double zTope,
        IReadOnlyList<(double Inf, double Sup)> nudos,
        double tramoMinimo)
    {
        var tramos = new List<SubTramo>();
        double cursor = zArranque;

        void Agregar(double inf, double sup, TipoTramo tipo)
        {
            if (sup - inf <= tramoMinimo) return;
            tramos.Add(new SubTramo(inf, sup, tipo));
        }

        foreach ((double inf, double sup) in nudos)
        {
            Agregar(cursor, inf, TipoTramo.Libre);
            Agregar(Math.Max(inf, cursor), sup, TipoTramo.DentroDeViga);
            cursor = Math.Max(cursor, sup);
        }

        Agregar(cursor, zTope, TipoTramo.Libre);

        return tramos;
    }
}
