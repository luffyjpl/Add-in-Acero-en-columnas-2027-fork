using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>Una planta del proyecto tal como se muestra en la lista.</summary>
    /// <param name="HasCotasView">Ya tiene su vista «Cotas &lt;planta&gt;».</param>
    public sealed record PlanEntry(long Id, string Name, string Level, int Scale, string Meta, bool HasCotasView);

    /// <summary>Lista las plantas (arquitectónicas y estructurales) ordenadas por nivel.</summary>
    internal static class PlanCatalog
    {
        private const double ParallelTolerance = 0.999;

        public static IReadOnlyList<PlanEntry> List(Document doc)
        {
            List<ViewPlan> views = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate)
                .ToList();

            var generated = views
                .Where(v => v.Name.StartsWith(PlanViewGenerator.NamePrefix))
                .Select(v => v.Name)
                .ToHashSet();

            return views
                .Where(v => v.GenLevel is not null
                    && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan)
                    // Las vistas que genera este add-in no son plantas de origen.
                    && !v.Name.StartsWith(PlanViewGenerator.NamePrefix))
                .OrderBy(v => v.GenLevel.Elevation)
                .ThenBy(v => v.Name)
                .Select(v => new PlanEntry(
                    v.Id.Value, v.Name, LevelText(v.GenLevel), v.Scale, Meta(v),
                    generated.Contains(PlanViewGenerator.ViewNameFor(v))))
                .ToList();
        }

        public static IReadOnlyList<string> LinearDimensionTypeNames(Document doc) =>
            new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.Linear && !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => t.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

        private static string LevelText(Level level)
        {
            double meters = Units.ToMeters(level.Elevation);
            return string.Format(CultureInfo.InvariantCulture, "N {0}{1:0.00}", meters >= 0 ? "+" : "", meters);
        }

        /// <summary>Resumen rápido para la lista: "3 × 6 ejes · 18 columnas".</summary>
        private static string Meta(ViewPlan view)
        {
            List<Line> grids = new FilteredElementCollector(view.Document, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(g => g.Curve)
                .OfType<Line>()
                .ToList();

            int vertical = grids.Count(l => Math.Abs(l.Direction.Y) > ParallelTolerance);
            int horizontal = grids.Count(l => Math.Abs(l.Direction.X) > ParallelTolerance);
            int columns = new FilteredElementCollector(view.Document, view.Id)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .GetElementCount();

            return $"{vertical} × {horizontal} ejes · {columns} columnas";
        }
    }
}
