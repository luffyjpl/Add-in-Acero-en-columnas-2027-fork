using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;

namespace CQEING.CotasPlanta.Domain
{
    public enum LegendSymbol { Chain, Column, Void, Beam }

    public sealed record LegendEntry(LegendSymbol Symbol, string Label, ChainKind? Kind = null);

    /// <summary>Contenido de la leyenda: el mismo en la vista previa y en el texto de Revit.</summary>
    public static class LegendContent
    {
        public static IReadOnlyList<LegendEntry> Build(PlanGeometry geometry, IReadOnlyList<ChainSpec> chains, CotasSettings settings)
        {
            var entries = chains
                .Select(c => c.Kind)
                .Distinct()
                .OrderBy(k => k)
                .Select(k => new LegendEntry(LegendSymbol.Chain, Palette.ChainLabel(k), k))
                .ToList();

            if (geometry.Columns.Count > 0)
            {
                entries.Add(new LegendEntry(LegendSymbol.Column, "Columna"));
            }

            if (settings.MarkVoids && geometry.Panels.Any(p => p.IsVoid))
            {
                entries.Add(new LegendEntry(LegendSymbol.Void, "Vacío (sin losa)"));
            }

            if (settings.BeamNaming == BeamNaming.Short)
            {
                entries.AddRange(geometry.Beams
                    .Where(b => b.Label is not null)
                    .GroupBy(b => (b.Label, b.Role, b.AxisName, b.Section))
                    .OrderBy(g => g.Key.Role).ThenBy(g => g.Key.Label)
                    .Select(g => new LegendEntry(LegendSymbol.Beam, $"{g.Key.Label} = {Describe(g.Key.Role, g.Key.AxisName)} ({g.Key.Section})")));
            }

            entries.AddRange(geometry.Beams
                .Where(b => b.Role == BeamRole.Vigueta)
                .Select(b => b.Section)
                .Distinct()
                .Select(section => new LegendEntry(LegendSymbol.Beam, $"Vigueta ({section}), sin rótulo")));

            return entries;
        }

        private static string Describe(BeamRole role, string? axisName) => role switch
        {
            BeamRole.Borde => "Viga de borde",
            _ when axisName is not null => "Viga eje " + axisName,
            _ => "Viga intermedia",
        };
    }
}
