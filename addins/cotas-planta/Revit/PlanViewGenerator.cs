using System;
using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <param name="AlreadyExisted">La vista ya existía y no se tocó.</param>
    /// <param name="Problems">Qué cadenas no se crearon o se crearon incompletas, y por qué.</param>
    public sealed record GenerationResult(string ViewName, long ViewId, bool AlreadyExisted, int Dimensions, IReadOnlyList<string> Problems);

    /// <summary>
    /// Crea la vista "Cotas &lt;planta&gt;" (un plano estructural duplicado) con todas las anotaciones.
    /// El nombre fijo es lo que permite que otros add-ins (p. ej. el de armar planos) la encuentren.
    /// </summary>
    internal static class PlanViewGenerator
    {
        public const string NamePrefix = "Cotas ";

        public static string ViewNameFor(ViewPlan source) => NamePrefix + source.Name;

        /// <summary>La vista de cotas de una planta, si ya existe.</summary>
        public static ViewPlan? Find(ViewPlan source) =>
            new FilteredElementCollector(source.Document)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name == ViewNameFor(source));

        public static GenerationResult Generate(ViewPlan source, CotasSettings settings)
        {
            // Si ya existe no se reemplaza: podría borrar lo que se haya ajustado a mano.
            if (Find(source) is { } existing)
            {
                return new GenerationResult(existing.Name, existing.Id.Value, AlreadyExisted: true, 0, Array.Empty<string>());
            }

            Document doc = source.Document;
            PlanGeometry geometry = PlanGeometryReader.Read(source, settings.OnAxisToleranceM);
            IReadOnlyList<ChainSpec> chains = ChainPlanner.Plan(geometry, settings);

            using var transaction = new Transaction(doc, "CQEING · Cotas de planta " + source.Name);
            transaction.Start();

            var view = (ViewPlan)doc.GetElement(source.Duplicate(ViewDuplicateOption.Duplicate));
            view.Name = ViewNameFor(source);
            view.ViewTemplateId = ElementId.InvalidElementId;
            // Sin recorte: la leyenda y las cotas exteriores no deben quedar cortadas.
            view.CropBoxActive = false;
            ShowOnlyPlanCategories(view);
            HideCadImports(view);
            ShowBubblesAtBothEnds(view);

            var canvas = new PlanCanvas(view, geometry, settings, AnnotationStyles.Ensure(doc, settings));
            GridExtender.Extend(canvas, chains);
            doc.Regenerate();
            ViewStyler.Apply(canvas);
            (int created, List<string> problems) = DimensionWriter.Write(canvas, chains);
            if (settings.MarkVoids)
            {
                VoidMarker.Mark(canvas);
            }

            BeamLabeler.Label(canvas);
            LegendWriter.Write(canvas, chains);

            transaction.Commit();
            return new GenerationResult(view.Name, view.Id.Value, AlreadyExisted: false, created, problems);
        }

        /// <summary>Lo único que se ve en una vista de cotas. Todo lo demás se oculta.</summary>
        private static readonly HashSet<BuiltInCategory> PlanCategories = new()
        {
            BuiltInCategory.OST_Grids,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Dimensions,
            BuiltInCategory.OST_TextNotes,
            BuiltInCategory.OST_Lines,
        };

        /// <summary>
        /// Oculta todas las categorías que no son de la planta de cotas: armadura, planos de referencia,
        /// marcas de sección, cámaras, cajas de sección, etc. Sin plantilla de vista aparecerían todas.
        /// </summary>
        private static void ShowOnlyPlanCategories(View view)
        {
            foreach (Category category in view.Document.Settings.Categories)
            {
                bool keep = PlanCategories.Contains((BuiltInCategory)category.Id.Value);
                if (!keep && view.CanCategoryBeHidden(category.Id))
                {
                    view.SetCategoryHidden(category.Id, true);
                }
            }
        }

        /// <summary>Los CAD importados o vinculados no van en la vista de cotas.</summary>
        private static void HideCadImports(View view)
        {
            List<ElementId> imports = new FilteredElementCollector(view.Document, view.Id)
                .OfClass(typeof(ImportInstance))
                .Where(e => e.CanBeHidden(view))
                .Select(e => e.Id)
                .ToList();

            if (imports.Count > 0)
            {
                view.HideElements(imports);
            }
        }

        /// <summary>Burbuja en los dos extremos de cada eje, como en la vista previa.</summary>
        private static void ShowBubblesAtBothEnds(View view)
        {
            foreach (Grid grid in new FilteredElementCollector(view.Document, view.Id).OfClass(typeof(Grid)).Cast<Grid>())
            {
                foreach (DatumEnds end in new[] { DatumEnds.End0, DatumEnds.End1 })
                {
                    if (grid.HasBubbleInView(end, view) && !grid.IsBubbleVisibleInView(end, view))
                    {
                        grid.ShowBubbleInView(end, view);
                    }
                }
            }
        }
    }
}
