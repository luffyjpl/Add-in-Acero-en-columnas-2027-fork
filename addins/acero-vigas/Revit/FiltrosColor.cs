using Autodesk.Revit.DB;
using CQEING.Vigas.Calculo.Generacion;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Vigas.Revit;

/// <summary>
/// Los colores por diámetro en Revit (#85, decisión 9 del plan docs/planes/2026-10-19-generar-acero-vigas-85.md): un
/// filtro de vista por diámetro, <c>"CQEING Acero #5"</c>, sobre la armadura estructural con la regla "diámetro de barra =
/// el del tipo del modelo", con el color de <see cref="ColoresDiametro"/> en el relleno y las líneas en negro
/// (el contorno de cada barra, Juan 21-oct). Los filtros quedan en el
/// proyecto y se pueden meter en las plantillas de vista; pintar elemento por elemento se pierde en cada vista nueva.
/// </summary>
internal static class FiltrosColor
{
    /// <summary>
    /// Crea o pone al día el filtro de cada diámetro y lo aplica a las vistas. Dentro de una transacción. Devuelve un aviso
    /// por cada vista que no los acepta (su plantilla controla los filtros).
    /// </summary>
    public static List<string> Aplicar(Document doc, IEnumerable<View> vistas, IEnumerable<int> diametros, TiposAcero tipos) =>
        // El trabajo lo hace FiltrosColorDiametro, que zapatas también usa (decisión 44 del plan de 2026-11-05).
        FiltrosColorDiametro.Aplicar(doc, vistas, diametros.Distinct()
            .Select(n => (n, tipos.Barra(n)?.BarNominalDiameter))
            .Where(b => b.Item2 is not null)
            .Select(b => (b.n, b.Item2!.Value)));
}
