using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CQEING.CotasPlanta.Domain;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>Lee de una vista de planta lo que el add-in necesita. Solo lectura.</summary>
    internal static class PlanGeometryReader
    {
        private const double ParallelTolerance = 0.999;
        private const double MaxJoistWidthM = 0.12;

        public static PlanGeometry Read(ViewPlan view, double onAxisToleranceM)
        {
            Document doc = view.Document;

            List<GridAxis> axes = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(ToAxis)
                .OfType<GridAxis>()
                .ToList();

            List<ColumnBox> columns = Instances(view, BuiltInCategory.OST_StructuralColumns)
                .Select(c => c.get_BoundingBox(null) is { } box ? new ColumnBox(c.Id.Value, ToRect(box)) : null)
                .OfType<ColumnBox>()
                .ToList();

            // Vigas y losas: solo las del nivel de la vista (la vista puede mostrar las de otros niveles).
            ElementId levelId = view.GenLevel.Id;

            List<BeamSegment> beams = Instances(view, BuiltInCategory.OST_StructuralFraming)
                .Where(b => ReferenceLevel(b) == levelId)
                .Select(ToBeam)
                .OfType<BeamSegment>()
                .ToList();

            List<SlabOutline> slabs = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f => f.LevelId == levelId)
                .Select(ToSlab)
                .OfType<SlabOutline>()
                .ToList();

            return PlanGeometry.Create(axes, columns, beams, slabs, onAxisToleranceM);
        }

        private static IEnumerable<FamilyInstance> Instances(View view, BuiltInCategory category) =>
            new FilteredElementCollector(view.Document, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>();

        /// <summary>Solo ejes rectos alineados con X o Y; los girados se ignoran.</summary>
        private static GridAxis? ToAxis(Grid grid)
        {
            if (grid.Curve is not Line line)
            {
                return null;
            }

            Point2 a = Units.ToPoint2(line.GetEndPoint(0));
            Point2 b = Units.ToPoint2(line.GetEndPoint(1));

            if (Math.Abs(line.Direction.X) > ParallelTolerance)
            {
                return new GridAxis(grid.Id.Value, grid.Name, Axis2.Y, a.Y, Math.Min(a.X, b.X), Math.Max(a.X, b.X));
            }

            if (Math.Abs(line.Direction.Y) > ParallelTolerance)
            {
                return new GridAxis(grid.Id.Value, grid.Name, Axis2.X, a.X, Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
            }

            return null;
        }

        private static BeamSegment? ToBeam(FamilyInstance beam)
        {
            if (beam.Location is not LocationCurve { Curve: Line line })
            {
                return null;
            }

            if (beam.get_BoundingBox(null) is not { } box)
            {
                return null;
            }

            // Ancho y alto se miden en la geometría, no en parámetros: cada familia los llama distinto.
            Axis2 along = Math.Abs(line.Direction.X) >= Math.Abs(line.Direction.Y) ? Axis2.X : Axis2.Y;
            Rect2 footprint = ToRect(box);
            double width = footprint.Max(along.Other()) - footprint.Min(along.Other());
            double height = Units.ToMeters(box.Max.Z - box.Min.Z);
            string section = string.Format(CultureInfo.InvariantCulture, "{0:0.00} x {1:0.00}", width, height);

            string typeName = beam.Document.GetElement(beam.GetTypeId())?.Name ?? "";
            return new BeamSegment(beam.Id.Value, along, footprint, section, RoleOf(typeName, width));
        }

        /// <summary>Nivel de referencia de la viga (o su nivel si el parámetro no existe).</summary>
        private static ElementId ReferenceLevel(FamilyInstance beam)
        {
            ElementId? id = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId();
            return id is not null && id != ElementId.InvalidElementId ? id : beam.LevelId;
        }

        /// <summary>
        /// Convención de nombres de tipo del proyecto: VT_ = vigueta, VB_ = viga de borde, VIG_/V = viga.
        /// Si el nombre no sigue la convención, una viga de 12 cm o menos se toma como vigueta.
        /// </summary>
        private static BeamRole RoleOf(string typeName, double width)
        {
            string name = typeName.Trim().ToUpperInvariant();
            if (name.StartsWith("VT")) return BeamRole.Vigueta;
            if (name.StartsWith("VB")) return BeamRole.Borde;
            if (name.StartsWith("V")) return BeamRole.Viga;
            return width <= MaxJoistWidthM ? BeamRole.Vigueta : BeamRole.Viga;
        }

        private static SlabOutline? ToSlab(Floor floor)
        {
            Reference? topReference = HostObjectUtils.GetTopFaces(floor).FirstOrDefault();
            if (topReference is null || floor.GetGeometryObjectFromReference(topReference) is not Face top)
            {
                return null;
            }

            List<IReadOnlyList<Point2>> loops = top.GetEdgesAsCurveLoops()
                .Select(loop => (IReadOnlyList<Point2>)loop
                    .SelectMany(curve => curve.Tessellate().Take(curve.Tessellate().Count - 1))
                    .Select(Units.ToPoint2)
                    .ToList())
                .Where(loop => loop.Count >= 3)
                .ToList();

            return loops.Count > 0 ? new SlabOutline(floor.Id.Value, loops) : null;
        }

        private static Rect2 ToRect(BoundingBoxXYZ box)
        {
            Point2 min = Units.ToPoint2(box.Min), max = Units.ToPoint2(box.Max);
            return new Rect2(min.X, min.Y, max.X, max.Y);
        }
    }
}
