using System;
using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    // Cada clase escribe una cosa en la vista. Todas trabajan dentro de la transacción de PlanViewGenerator.

    /// <summary>Contexto común: la vista, la configuración, los estilos y la cota Z donde se dibuja.</summary>
    internal sealed record PlanCanvas(ViewPlan View, PlanGeometry Geometry, CotasSettings Settings, AnnotationStyles Styles)
    {
        public Document Doc => View.Document;

        /// <summary>Z del plano de la vista (coordenadas internas).</summary>
        public double Z => View.GenLevel.ProjectElevation;

        public XYZ Point(double xMeters, double yMeters) => new(Units.ToFeet(xMeters), Units.ToFeet(yMeters), Z);

        public XYZ Point(Point2 p) => Point(p.X, p.Y);

        public double Paper(double mm) => CotasSettings.PaperToModel(mm, View.Scale);

        /// <summary>Distancia (m) del borde de la estructura al final de los ejes de ese lado: por fuera de todas las cotas.</summary>
        public double GridEndDistance(IReadOnlyList<ChainSpec> chains, PlanSide side) =>
            Paper(SideInfo.ChainsDepthMm(chains, side, Settings) + GridGapMm);

        private const double GridGapMm = 6;
    }

    /// <summary>Crea cada cadena como una cota de Revit con el tipo de su color.</summary>
    internal static class DimensionWriter
    {
        /// <returns>Cotas creadas y una descripción de cada problema encontrado.</returns>
        public static (int Created, List<string> Problems) Write(PlanCanvas canvas, IReadOnlyList<ChainSpec> chains)
        {
            int created = 0;
            var problems = new List<string>();

            foreach (ChainSpec chain in chains)
            {
                string name = $"{SideName(chain.Side)} · {Palette.ChainLabel(chain.Kind)}";
                (ReferenceArray references, List<ChainRef> usable, int missingRequired, int skippedJoists) = Resolve(canvas, chain);

                if (skippedJoists > 0)
                {
                    problems.Add($"{name}: {skippedJoists} viguetas sin eje central acotable, se omitieron.");
                }

                // Una cadena de viguetas sin viguetas repetiría la de eje a eje.
                if (chain.Kind == ChainKind.Viguetas && usable.All(r => r.Kind == RefKind.Axis))
                {
                    problems.Add($"{name}: no se creó, ninguna vigueta se pudo acotar.");
                    continue;
                }

                if (missingRequired > 0 || references.Size < 2)
                {
                    problems.Add($"{name}: no se creó, faltan {Math.Max(missingRequired, 2 - references.Size)} referencias.");
                    continue;
                }

                double at = SideInfo.LinePosition(canvas.Geometry.Extents, chain.Side, chain.Ring, canvas.Settings, canvas.View.Scale);
                double from = usable[0].Position, to = usable[^1].Position;
                Line line = chain.Along == Axis2.X
                    ? Line.CreateBound(canvas.Point(from, at), canvas.Point(to, at))
                    : Line.CreateBound(canvas.Point(at, from), canvas.Point(at, to));

                try
                {
                    Dimension dimension = canvas.Doc.Create.NewDimension(canvas.View, line, references, canvas.Styles.For(chain.Kind));
                    created++;
                    if (chain.TextAt is not null || chain.TextRow > 0)
                    {
                        double textAt = chain.TextAt ?? (from + to) / 2;
                        double rowFeet = Units.ToFeet(canvas.Paper(canvas.Settings.TextSizeMm * TextRowFactor) * chain.TextRow);
                        if (!TryMoveText(canvas.Doc, dimension, line, Units.ToFeet(textAt - from), rowFeet))
                        {
                            problems.Add($"{name}: comparte línea con otro voladizo y su texto no se pudo correr; muévelo a mano.");
                        }
                    }
                }
                catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException or Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    problems.Add($"{name}: Revit no la aceptó ({ex.Message}).");
                }
            }

            return (created, problems);
        }

        /// <summary>
        /// Convierte cada punto en una referencia de Revit (eje, cara o eje de vigueta). Si a una vigueta
        /// le falta su eje se omite solo esa; si falta otro punto, la cadena no se crea.
        /// </summary>
        private static (ReferenceArray References, List<ChainRef> Usable, int MissingRequired, int SkippedJoists) Resolve(
            PlanCanvas canvas, ChainSpec chain)
        {
            XYZ along = chain.Along == Axis2.X ? XYZ.BasisX : XYZ.BasisY;
            var array = new ReferenceArray();
            var usable = new List<ChainRef>();
            int missingRequired = 0, skippedJoists = 0;

            foreach (ChainRef point in chain.Refs)
            {
                Element? element = canvas.Doc.GetElement(new ElementId(point.ElementId));
                Reference? reference = point.Kind switch
                {
                    RefKind.Axis => element is Grid grid ? new Reference(grid) : null,
                    RefKind.ElementCenter => element is FamilyInstance beam ? CenterLineOf(beam) : null,
                    _ => element is null ? null : FaceReferences.Find(element, along, point.AtMin, canvas.View),
                };

                if (reference is not null)
                {
                    array.Append(reference);
                    usable.Add(point);
                }
                // Solo el eje de una vigueta es prescindible; en una cadena unida, la cara de un voladizo no lo es.
                else if (point.Kind == RefKind.ElementCenter)
                {
                    skippedJoists++;
                }
                else
                {
                    missingRequired++;
                }
            }

            return (array, usable, missingRequired, skippedJoists);
        }

        /// <summary>Alto de un renglón de texto, en veces la altura del texto (texto + aire).</summary>
        private const double TextRowFactor = 1.4;

        /// <summary>
        /// Mueve el texto de una cota de un solo tramo: queda a <paramref name="alongFeet"/> del inicio de
        /// la línea y <paramref name="awayFeet"/> más lejos de ella, del mismo lado donde Revit lo puso.
        /// Así dos voladizos del mismo eje comparten línea con sus textos lado a lado.
        /// </summary>
        private static bool TryMoveText(Document doc, Dimension dimension, Line line, double alongFeet, double awayFeet)
        {
            if (dimension.NumberOfSegments > 0)
            {
                return false;
            }

            try
            {
                doc.Regenerate();
                XYZ text = dimension.TextPosition;
                XYZ start = line.GetEndPoint(0);
                XYZ offset = text - (start + line.Direction * (text - start).DotProduct(line.Direction));
                XYZ away = offset.GetLength() > 1e-9 ? offset.Normalize() * awayFeet : XYZ.Zero;
                dimension.TextPosition = start + line.Direction * alongFeet + offset + away;
                return true;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException or Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Eje de una viga en planta. En las familias de vigas es el plano "Center (Front/Back)":
        /// corre a lo largo de la viga por su centro (verificado en el modelo con VT_10X35).
        /// </summary>
        private static Reference? CenterLineOf(FamilyInstance beam) =>
            beam.GetReferences(FamilyInstanceReferenceType.CenterFrontBack).FirstOrDefault();

        public static string SideName(PlanSide side) => side switch
        {
            PlanSide.Top => "Arriba",
            PlanSide.Bottom => "Abajo",
            PlanSide.Left => "Izquierda",
            _ => "Derecha",
        };
    }

    /// <summary>
    /// Alarga (o acorta) los ejes en esta vista para que sus burbujas queden por fuera de todas
    /// las cotas, como en la vista previa. Además, Revit no acota un eje más allá de donde termina.
    /// </summary>
    internal static class GridExtender
    {
        public static void Extend(PlanCanvas canvas, IReadOnlyList<ChainSpec> chains)
        {
            Rect2 ext = canvas.Geometry.Extents;
            foreach (Grid grid in new FilteredElementCollector(canvas.Doc, canvas.View.Id).OfClass(typeof(Grid)).Cast<Grid>())
            {
                GridAxis? axis = canvas.Geometry.Axes.FirstOrDefault(a => a.Id == grid.Id.Value);
                if (axis is null || grid.GetCurvesInView(DatumExtentType.ViewSpecific, canvas.View).FirstOrDefault() is not Line current)
                {
                    continue;
                }

                Axis2 along = axis.Fixed.Other();
                (PlanSide low, PlanSide high) = along == Axis2.Y ? (PlanSide.Bottom, PlanSide.Top) : (PlanSide.Left, PlanSide.Right);
                double from = ext.Min(along) - canvas.GridEndDistance(chains, low);
                double to = ext.Max(along) + canvas.GridEndDistance(chains, high);

                // Mismo sentido y misma Z que la línea actual (Revit lo exige).
                double z = current.GetEndPoint(0).Z;
                XYZ At(double value) => along == Axis2.Y
                    ? new XYZ(Units.ToFeet(axis.Position), Units.ToFeet(value), z)
                    : new XYZ(Units.ToFeet(value), Units.ToFeet(axis.Position), z);
                bool forward = current.Direction.DotProduct(along == Axis2.Y ? XYZ.BasisY : XYZ.BasisX) > 0;
                Line extended = forward ? Line.CreateBound(At(from), At(to)) : Line.CreateBound(At(to), At(from));

                try
                {
                    grid.SetDatumExtentType(DatumEnds.End0, canvas.View, DatumExtentType.ViewSpecific);
                    grid.SetDatumExtentType(DatumEnds.End1, canvas.View, DatumExtentType.ViewSpecific);
                    grid.SetCurveInView(DatumExtentType.ViewSpecific, canvas.View, extended);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Un eje que Revit no deja cambiar se queda como está.
                }
            }
        }
    }

    /// <summary>Columnas en naranja y losa en gris tenue y transparente, solo en esta vista.</summary>
    internal static class ViewStyler
    {
        public static void Apply(PlanCanvas canvas)
        {
            ElementId solid = new FilteredElementCollector(canvas.Doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .First(f => f.GetFillPattern().IsSolidFill)
                .Id;

            OverrideGraphicSettings column = Filled(solid, Palette.Column);
            foreach (ColumnBox c in canvas.Geometry.Columns)
            {
                canvas.View.SetElementOverrides(new ElementId(c.Id), column);
            }

            if (!canvas.Settings.TintSlab)
            {
                return;
            }

            // Transparente para que se vean las viguetas y vigas que quedan debajo.
            OverrideGraphicSettings slab = Filled(solid, Palette.SlabTint)
                .SetSurfaceTransparency(canvas.Settings.SlabTransparency)
                .SetProjectionLineColor(AnnotationStyles.ToRevitColor(Palette.SlabEdge));
            foreach (SlabOutline s in canvas.Geometry.Slabs)
            {
                canvas.View.SetElementOverrides(new ElementId(s.Id), slab);
            }
        }

        private static OverrideGraphicSettings Filled(ElementId solidPattern, Rgb rgb)
        {
            Color color = AnnotationStyles.ToRevitColor(rgb);
            return new OverrideGraphicSettings()
                .SetSurfaceForegroundPatternId(solidPattern).SetSurfaceForegroundPatternColor(color)
                .SetCutForegroundPatternId(solidPattern).SetCutForegroundPatternColor(color);
        }
    }

    /// <summary>Una X en cada recuadro entre ejes que no tiene losa ni viguetas.</summary>
    internal static class VoidMarker
    {
        public static int Mark(PlanCanvas canvas)
        {
            double inset = (canvas.Geometry.Beams.Count > 0 ? canvas.Geometry.Beams.Max(b => b.Width) / 2 : 0.10) + 0.05;
            int marked = 0;

            foreach (GridPanel panel in canvas.Geometry.Panels.Where(p => p.IsVoid))
            {
                Rect2 r = panel.Box.Deflate(inset);
                Line(canvas, canvas.Point(r.MinX, r.MinY), canvas.Point(r.MaxX, r.MaxY));
                Line(canvas, canvas.Point(r.MinX, r.MaxY), canvas.Point(r.MaxX, r.MinY));
                marked++;
            }

            return marked;
        }

        private static void Line(PlanCanvas canvas, XYZ a, XYZ b)
        {
            DetailCurve curve = canvas.Doc.Create.NewDetailCurve(canvas.View, Autodesk.Revit.DB.Line.CreateBound(a, b));
            curve.LineStyle = canvas.Styles.VoidLine;
        }
    }

    /// <summary>Rótulo de cada viga (V1, VI-1, VBD o su sección), uno por vano. Las viguetas no llevan rótulo.</summary>
    internal static class BeamLabeler
    {
        public static int Label(PlanCanvas canvas)
        {
            int labeled = 0;
            foreach (BeamSegment beam in canvas.Geometry.Beams.Where(b => b.Label is not null))
            {
                string text = canvas.Settings.BeamNaming == BeamNaming.Short ? beam.Label! : $"({beam.Section})";
                var options = new TextNoteOptions(canvas.Styles.TextTypeId)
                {
                    HorizontalAlignment = HorizontalTextAlignment.Center,
                    VerticalAlignment = VerticalTextAlignment.Middle,
                    Rotation = beam.Along == Axis2.Y ? Math.PI / 2 : 0,
                };

                foreach (Point2 at in canvas.Geometry.LabelPoints(beam))
                {
                    TextNote.Create(canvas.Doc, canvas.View.Id, canvas.Point(at), text, options);
                    labeled++;
                }
            }

            return labeled;
        }
    }

    /// <summary>La leyenda como un bloque de texto a la derecha, por fuera de las burbujas de los ejes.</summary>
    internal static class LegendWriter
    {
        /// <summary>Espacio para la burbuja del eje (su diámetro) más un margen.</summary>
        private const double BubbleAndGapMm = 20;

        public static void Write(PlanCanvas canvas, IReadOnlyList<ChainSpec> chains)
        {
            IReadOnlyList<LegendEntry> entries = LegendContent.Build(canvas.Geometry, chains, canvas.Settings);
            if (entries.Count == 0)
            {
                return;
            }

            IEnumerable<string> lines = new[] { "LEYENDA" }.Concat(entries.Select(Line));

            double x = canvas.Geometry.Extents.MaxX + canvas.GridEndDistance(chains, PlanSide.Right) + canvas.Paper(BubbleAndGapMm);
            double y = canvas.Geometry.Extents.MaxY;

            var options = new TextNoteOptions(canvas.Styles.TextTypeId)
            {
                HorizontalAlignment = HorizontalTextAlignment.Left,
                VerticalAlignment = VerticalTextAlignment.Top,
            };
            TextNote.Create(canvas.Doc, canvas.View.Id, canvas.Point(x, y), string.Join("\r", lines), options);
        }

        private static string Line(LegendEntry entry) => entry.Symbol switch
        {
            LegendSymbol.Chain when entry.Kind is { } kind => $"{Palette.ChainColorName(kind)}: {entry.Label}",
            LegendSymbol.Column => "Naranja: columna",
            LegendSymbol.Void => "X: vacío (sin losa)",
            _ => entry.Label,
        };
    }
}
