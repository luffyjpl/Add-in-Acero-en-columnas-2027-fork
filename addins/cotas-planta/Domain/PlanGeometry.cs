using System;
using System.Collections.Generic;
using System.Linq;

namespace CQEING.CotasPlanta.Domain
{
    // Modelo de la planta en metros, independiente de Revit:
    // lo usan la vista previa y el planificador de cotas.

    public enum Axis2 { X, Y }

    public static class Axis2Extensions
    {
        public static Axis2 Other(this Axis2 axis) => axis == Axis2.X ? Axis2.Y : Axis2.X;
    }

    public readonly record struct Point2(double X, double Y)
    {
        public double Get(Axis2 axis) => axis == Axis2.X ? X : Y;
    }

    public readonly record struct Rect2(double MinX, double MinY, double MaxX, double MaxY)
    {
        public Point2 Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);

        public double Min(Axis2 axis) => axis == Axis2.X ? MinX : MinY;

        public double Max(Axis2 axis) => axis == Axis2.X ? MaxX : MaxY;

        public Rect2 Union(Rect2 other) => new(
            Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

        public Rect2 Deflate(double d) => new(MinX + d, MinY + d, MaxX - d, MaxY - d);

        public static Rect2 FromPoints(IEnumerable<Point2> points)
        {
            List<Point2> list = points.ToList();
            return new Rect2(list.Min(p => p.X), list.Min(p => p.Y), list.Max(p => p.X), list.Max(p => p.Y));
        }
    }

    /// <summary>
    /// Eje recto. <see cref="Fixed"/> es la coordenada constante:
    /// un eje con Fixed = X es la línea vertical x = <see cref="Position"/>.
    /// </summary>
    public sealed record GridAxis(long Id, string Name, Axis2 Fixed, double Position, double From, double To);

    public sealed record ColumnBox(long Id, Rect2 Box);

    /// <summary>Papel de la viga en la planta (se deduce del nombre del tipo: VIG_, VT_, VB_).</summary>
    public enum BeamRole { Viga, Vigueta, Borde }

    /// <param name="Along">Dirección de la viga (sale de su línea de ubicación).</param>
    /// <param name="Box">Huella real en planta (de la geometría: la línea de ubicación puede ir por un borde).</param>
    /// <param name="AxisName">Eje sobre el que está la viga, si está sobre uno.</param>
    /// <param name="Label">Nombre corto en planta (V1, VI-1, VB). Null = sin rótulo (viguetas).</param>
    public sealed record BeamSegment(
        long Id, Axis2 Along, Rect2 Box, string Section, BeamRole Role,
        string? AxisName = null, string? Label = null)
    {
        public Point2 Mid => Box.Center;

        public double Width => Box.Max(Along.Other()) - Box.Min(Along.Other());
    }

    public sealed record SlabOutline(long Id, IReadOnlyList<IReadOnlyList<Point2>> Loops)
    {
        public Rect2 Bounds => Rect2.FromPoints(Loops.SelectMany(loop => loop));

        /// <summary>Punto dentro de la losa (regla par-impar: los huecos quedan fuera).</summary>
        public bool Contains(Point2 p)
        {
            bool inside = false;
            foreach (IReadOnlyList<Point2> loop in Loops)
            {
                for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
                {
                    Point2 a = loop[i], b = loop[j];
                    bool crosses = (a.Y > p.Y) != (b.Y > p.Y)
                        && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
                    if (crosses)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }
    }

    /// <summary>Recuadro entre ejes consecutivos. Es vacío si ninguna losa lo cubre.</summary>
    public sealed record GridPanel(Rect2 Box, bool IsVoid);

    /// <summary>Cualquier elemento con volumen: sirve para encontrar los extremos (voladizos).</summary>
    public sealed record SolidBox(long Id, Rect2 Box);

    public sealed class PlanGeometry
    {
        private PlanGeometry(
            IReadOnlyList<GridAxis> axes,
            IReadOnlyList<ColumnBox> columns,
            IReadOnlyList<BeamSegment> beams,
            IReadOnlyList<SlabOutline> slabs,
            IReadOnlyList<GridPanel> panels,
            Rect2 extents)
        {
            Axes = axes;
            Columns = columns;
            Beams = beams;
            Slabs = slabs;
            Panels = panels;
            Extents = extents;
        }

        public IReadOnlyList<GridAxis> Axes { get; }
        public IReadOnlyList<ColumnBox> Columns { get; }
        public IReadOnlyList<BeamSegment> Beams { get; }
        public IReadOnlyList<SlabOutline> Slabs { get; }
        public IReadOnlyList<GridPanel> Panels { get; }

        /// <summary>Caja que encierra toda la estructura (o los ejes si no hay elementos).</summary>
        public Rect2 Extents { get; }

        public bool IsEmpty => Axes.Count == 0;

        public IEnumerable<SolidBox> Solids =>
            Columns.Select(c => new SolidBox(c.Id, c.Box))
                .Concat(Beams.Select(b => new SolidBox(b.Id, b.Box)))
                .Concat(Slabs.Select(s => new SolidBox(s.Id, s.Bounds)));

        /// <summary>
        /// Dónde va el rótulo de una viga: el centro de cada vano entre ejes que cruza,
        /// para que nunca caiga sobre una columna. Los vanos muy cortos no llevan rótulo.
        /// </summary>
        public IReadOnlyList<Point2> LabelPoints(BeamSegment beam, double minSpanM = 0.6)
        {
            Axis2 along = beam.Along;
            double from = beam.Box.Min(along), to = beam.Box.Max(along);
            double line = beam.Mid.Get(along.Other());

            List<double> stops = AxesFixed(along)
                .Select(a => a.Position)
                .Where(p => p > from && p < to)
                .Prepend(from)
                .Append(to)
                .ToList();

            var points = new List<Point2>();
            for (int i = 0; i < stops.Count - 1; i++)
            {
                if (stops[i + 1] - stops[i] >= minSpanM)
                {
                    double middle = (stops[i] + stops[i + 1]) / 2;
                    points.Add(along == Axis2.X ? new Point2(middle, line) : new Point2(line, middle));
                }
            }

            return points.Count > 0 ? points : new[] { beam.Mid };
        }

        /// <summary>Ejes con esa coordenada constante, ordenados por posición.</summary>
        public IReadOnlyList<GridAxis> AxesFixed(Axis2 fixedCoordinate) =>
            Axes.Where(a => a.Fixed == fixedCoordinate).OrderBy(a => a.Position).ToList();

        public static PlanGeometry Create(
            IReadOnlyList<GridAxis> axes,
            IReadOnlyList<ColumnBox> columns,
            IReadOnlyList<BeamSegment> beams,
            IReadOnlyList<SlabOutline> slabs,
            double onAxisTolerance)
        {
            List<BeamSegment> namedBeams = NameBeams(axes, beams, onAxisTolerance);
            IReadOnlyList<GridPanel> panels = BuildPanels(axes, slabs, namedBeams);
            Rect2 extents = BuildExtents(axes, columns, namedBeams, slabs);
            return new PlanGeometry(axes, columns, namedBeams, slabs, panels, extents);
        }

        /// <summary>
        /// V1, VA… para vigas sobre un eje; VI-1, VI-2… para vigas fuera de eje
        /// (los tramos de una misma línea comparten nombre); VB para bordes; viguetas sin rótulo.
        /// </summary>
        private static List<BeamSegment> NameBeams(IReadOnlyList<GridAxis> axes, IReadOnlyList<BeamSegment> beams, double tolerance)
        {
            List<BeamSegment> withAxis = beams
                .Select(b => b with { AxisName = AxisUnder(axes, b, tolerance)?.Name })
                .ToList();

            var intermediateNames = withAxis
                .Where(b => b.Role == BeamRole.Viga && b.AxisName is null)
                .Select(LineKey)
                .Distinct()
                .OrderBy(k => k.Along).ThenBy(k => k.Coordinate)
                .Select((key, index) => (key, name: "VI-" + (index + 1)))
                .ToDictionary(x => x.key, x => x.name);

            return withAxis.Select(b => b with
            {
                Label = b.Role switch
                {
                    BeamRole.Vigueta => null,
                    // "VB" chocaría con la viga del eje B.
                    BeamRole.Borde => "VBD",
                    _ => b.AxisName is not null ? "V" + b.AxisName : intermediateNames[LineKey(b)],
                },
            }).ToList();
        }

        /// <summary>Identifica la línea de una viga: su dirección y su coordenada fija redondeada al cm.</summary>
        private static (Axis2 Along, double Coordinate) LineKey(BeamSegment beam) =>
            (beam.Along, Math.Round(beam.Mid.Get(beam.Along.Other()), 2));

        private static GridAxis? AxisUnder(IEnumerable<GridAxis> axes, BeamSegment beam, double tolerance)
        {
            Axis2 fixedCoordinate = beam.Along.Other();
            double coordinate = beam.Mid.Get(fixedCoordinate);
            return axes.FirstOrDefault(a => a.Fixed == fixedCoordinate && Math.Abs(a.Position - coordinate) <= tolerance);
        }

        /// <summary>
        /// Recuadros entre ejes. Es vacío si no lo cubre ninguna losa y no tiene viguetas
        /// (un recuadro con viguetas es aligerado, no vacío).
        /// </summary>
        private static IReadOnlyList<GridPanel> BuildPanels(
            IReadOnlyList<GridAxis> axes, IReadOnlyList<SlabOutline> slabs, IReadOnlyList<BeamSegment> beams)
        {
            List<Rect2> joists = beams.Where(b => b.Role == BeamRole.Vigueta).Select(b => b.Box).ToList();
            bool HasJoists(Rect2 box) => joists.Any(j =>
                j.MaxX > box.MinX && j.MinX < box.MaxX && j.MaxY > box.MinY && j.MinY < box.MaxY);

            double[] xs = axes.Where(a => a.Fixed == Axis2.X).Select(a => a.Position).OrderBy(v => v).ToArray();
            double[] ys = axes.Where(a => a.Fixed == Axis2.Y).Select(a => a.Position).OrderBy(v => v).ToArray();
            var panels = new List<GridPanel>();

            for (int i = 0; i < xs.Length - 1; i++)
            {
                for (int j = 0; j < ys.Length - 1; j++)
                {
                    var box = new Rect2(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    // Sin losas (p. ej. cimentación) no se marca nada como vacío.
                    bool isVoid = slabs.Count > 0 && !slabs.Any(s => s.Contains(box.Center)) && !HasJoists(box);
                    panels.Add(new GridPanel(box, isVoid));
                }
            }

            return panels;
        }

        private static Rect2 BuildExtents(
            IReadOnlyList<GridAxis> axes,
            IEnumerable<ColumnBox> columns,
            IEnumerable<BeamSegment> beams,
            IEnumerable<SlabOutline> slabs)
        {
            List<Rect2> boxes = columns.Select(c => c.Box)
                .Concat(beams.Select(b => b.Box))
                .Concat(slabs.Select(s => s.Bounds))
                .ToList();

            if (boxes.Count > 0)
            {
                return boxes.Aggregate((a, b) => a.Union(b));
            }

            if (axes.Count == 0)
            {
                return new Rect2(0, 0, 1, 1);
            }

            IEnumerable<Point2> axisPoints = axes.SelectMany(a => a.Fixed == Axis2.X
                ? new[] { new Point2(a.Position, a.From), new Point2(a.Position, a.To) }
                : new[] { new Point2(a.From, a.Position), new Point2(a.To, a.Position) });
            return Rect2.FromPoints(axisPoints);
        }
    }
}
