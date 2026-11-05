using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;

namespace CQEING.CotasPlanta.UI
{
    /// <summary>Lo que dibuja la vista previa.</summary>
    public sealed record PreviewScene(PlanGeometry Geometry, IReadOnlyList<ChainSpec> Chains, CotasSettings Settings)
    {
        public int ViewScale => Settings.ViewScale;
    }

    /// <summary>
    /// Vista previa de la planta con las cotas tal como quedarán.
    /// Rueda del mouse: zoom hacia el cursor. Arrastrar: mover. Doble clic: volver a ajustar.
    /// </summary>
    public sealed class PlanPreview : FrameworkElement
    {
        private const double ZoomStep = 1.2;
        private const double MaxZoom = 20;

        public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
            nameof(Scene), typeof(PreviewScene), typeof(PlanPreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSceneChanged));

        /// <summary>Zoom y desplazamiento aplicados sobre el dibujo ajustado.</summary>
        private Matrix _view = Matrix.Identity;
        private Point? _dragFrom;

        public PlanPreview()
        {
            ClipToBounds = true;
            Cursor = System.Windows.Input.Cursors.SizeAll;
        }

        public PreviewScene? Scene
        {
            get => (PreviewScene?)GetValue(SceneProperty);
            set => SetValue(SceneProperty, value);
        }

        /// <summary>Al cambiar de planta se vuelve a ajustar; al cambiar solo la configuración se conserva el zoom.</summary>
        private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PreviewScene old && e.NewValue is PreviewScene current && ReferenceEquals(old.Geometry, current.Geometry))
            {
                return;
            }

            ((PlanPreview)d)._view = Matrix.Identity;
        }

        protected override void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
            Point at = e.GetPosition(this);
            double target = Math.Clamp(_view.M11 * (e.Delta > 0 ? ZoomStep : 1 / ZoomStep), 1, MaxZoom);
            double factor = target / _view.M11;
            _view.ScaleAt(factor, factor, at.X, at.Y);
            if (target <= 1)
            {
                _view = Matrix.Identity;
            }

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _view = Matrix.Identity;
                InvalidateVisual();
                return;
            }

            _dragFrom = e.GetPosition(this);
            CaptureMouse();
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (_dragFrom is not { } from)
            {
                return;
            }

            Point to = e.GetPosition(this);
            _view.Translate(to.X - from.X, to.Y - from.Y);
            _dragFrom = to;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragFrom = null;
            ReleaseMouseCapture();
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
            if (RenderSize.Width < 50 || RenderSize.Height < 50)
            {
                return;
            }

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (Scene is null || Scene.Geometry.IsEmpty)
            {
                var message = new FormattedText(
                    "Esta planta no tiene ejes ortogonales para acotar.",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13,
                    Ink.Muted, pixelsPerDip);
                dc.DrawText(message, new Point((RenderSize.Width - message.Width) / 2, RenderSize.Height / 2));
                return;
            }

            dc.PushTransform(new MatrixTransform(_view));
            new PreviewPainter(dc, Scene, RenderSize, pixelsPerDip).Paint();
            dc.Pop();
        }
    }

    /// <summary>Pinceles compartidos por la vista previa y la leyenda.</summary>
    internal static class Ink
    {
        public static readonly Brush Text = Frozen(Color.FromRgb(27, 31, 36));
        public static readonly Brush Muted = Frozen(Color.FromRgb(89, 98, 110));
        public static readonly Brush Column = Frozen(ToColor(Palette.Column));
        public static readonly Brush SlabTint = Frozen(ToColor(Palette.SlabTint));
        public static readonly Pen ColumnEdge = FrozenPen(Color.FromRgb(122, 62, 8), 0.7);
        public static readonly Pen Beam = FrozenPen(Color.FromRgb(124, 133, 143), 0.8);
        public static readonly Pen Joist = FrozenPen(Color.FromRgb(184, 190, 198), 0.5);
        public static readonly Pen SlabEdge = FrozenPen(ToColor(Palette.SlabEdge), 0.6);
        public static readonly Pen VoidMark = FrozenPen(ToColor(Palette.VoidMark), 0.6);
        public static readonly Pen Bubble = FrozenPen(Color.FromRgb(27, 31, 36), 0.9);
        public static readonly Pen Grid = FrozenDashed(Color.FromRgb(138, 147, 158), 0.7);

        public static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

        /// <summary>Pincel de una cadena con el color que le da la configuración (el de su tipo o negro).</summary>
        public static Brush Chain(Rgb color)
        {
            if (!ChainBrushes.TryGetValue(color, out Brush? brush))
            {
                brush = Frozen(ToColor(color));
                ChainBrushes[color] = brush;
            }

            return brush;
        }

        private static readonly Dictionary<Rgb, Brush> ChainBrushes = new();

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen FrozenPen(Color color, double thickness)
        {
            var pen = new Pen(Frozen(color), thickness);
            pen.Freeze();
            return pen;
        }

        private static Pen FrozenDashed(Color color, double thickness)
        {
            var pen = new Pen(Frozen(color), thickness) { DashStyle = new DashStyle(new double[] { 12, 4, 2, 4 }, 0) };
            pen.Freeze();
            return pen;
        }
    }

    /// <summary>
    /// Dibuja una escena. Trabaja en metros de modelo; los tamaños "de papel"
    /// (texto, separaciones, burbujas) se convierten con la escala de la vista.
    /// </summary>
    internal sealed class PreviewPainter
    {
        private const double PaddingPx = 12;
        private const double BubbleRadiusMm = 4;
        private const double BubbleGapMm = 8;
        private const double DimTextMm = 2.5;
        private const double BeamTextMm = 2.5;

        /// <summary>Un elemento a más de esto (m) del contorno lleva línea de referencia.</summary>
        private const double InsideThresholdM = 0.30;

        private static readonly Typeface Regular = new("Segoe UI");
        private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private readonly DrawingContext _dc;
        private readonly PreviewScene _scene;
        private readonly double _pixelsPerDip;
        private readonly Rect2 _world;
        private readonly double _scale;
        private readonly double _originX;
        private readonly double _originY;

        public PreviewPainter(DrawingContext dc, PreviewScene scene, Size size, double pixelsPerDip)
        {
            _dc = dc;
            _scene = scene;
            _pixelsPerDip = pixelsPerDip;

            Rect2 ext = scene.Geometry.Extents;
            double Margin(PlanSide side) => BubbleDistance(side) + Paper(BubbleRadiusMm + 2);
            _world = new Rect2(
                ext.MinX - Margin(PlanSide.Left), ext.MinY - Margin(PlanSide.Bottom),
                ext.MaxX + Margin(PlanSide.Right), ext.MaxY + Margin(PlanSide.Top));

            double width = _world.MaxX - _world.MinX, height = _world.MaxY - _world.MinY;
            _scale = Math.Min((size.Width - 2 * PaddingPx) / width, (size.Height - 2 * PaddingPx) / height);
            _originX = (size.Width - width * _scale) / 2;
            _originY = (size.Height - height * _scale) / 2;
        }

        private PlanGeometry Geometry => _scene.Geometry;
        private CotasSettings Settings => _scene.Settings;

        public void Paint()
        {
            PaintSlabs();
            PaintVoids();
            PaintBeams();
            PaintColumns();
            PaintAxes();
            foreach (ChainSpec chain in _scene.Chains)
            {
                PaintChain(chain);
            }
        }

        // ---------- conversión de coordenadas ----------

        /// <summary>Modelo (m, Y hacia arriba) → pantalla (px, Y hacia abajo).</summary>
        private Point P(double x, double y) => new(_originX + (x - _world.MinX) * _scale, _originY + (_world.MaxY - y) * _scale);

        private Point P(Point2 p) => P(p.X, p.Y);

        /// <summary>Milímetros de papel → metros de modelo.</summary>
        private double Paper(double mm) => CotasSettings.PaperToModel(mm, _scene.ViewScale);

        /// <summary>Milímetros de papel → píxeles.</summary>
        private double Px(double mm) => Paper(mm) * _scale;

        private int RingCount(PlanSide side) => SideInfo.RingCount(_scene.Chains, side);

        /// <summary>Distancia (m) del borde de la estructura al centro de las burbujas de ese lado.</summary>
        private double BubbleDistance(PlanSide side)
        {
            int rings = RingCount(side);
            double chainsMm = rings > 0 ? Settings.FirstOffsetMm + (rings - 1) * Settings.SpacingMm + DimTextMm * 1.6 : 0;
            return Paper(chainsMm + BubbleGapMm + BubbleRadiusMm);
        }

        // ---------- elementos ----------

        private void PaintSlabs()
        {
            Brush fill = Settings.TintSlab ? Ink.SlabTint : Brushes.Transparent;
            foreach (SlabOutline slab in Geometry.Slabs)
            {
                var shape = new StreamGeometry { FillRule = FillRule.EvenOdd };
                using (StreamGeometryContext ctx = shape.Open())
                {
                    foreach (IReadOnlyList<Point2> loop in slab.Loops)
                    {
                        ctx.BeginFigure(P(loop[0]), isFilled: true, isClosed: true);
                        ctx.PolyLineTo(loop.Skip(1).Select(P).ToList(), isStroked: true, isSmoothJoin: false);
                    }
                }

                shape.Freeze();
                _dc.DrawGeometry(fill, Ink.SlabEdge, shape);
            }
        }

        private void PaintVoids()
        {
            if (!Settings.MarkVoids)
            {
                return;
            }

            double inset = (Geometry.Beams.Count > 0 ? Geometry.Beams.Max(b => b.Width) / 2 : 0.10) + 0.05;
            foreach (GridPanel panel in Geometry.Panels.Where(p => p.IsVoid))
            {
                Rect2 r = panel.Box.Deflate(inset);
                _dc.DrawLine(Ink.VoidMark, P(r.MinX, r.MinY), P(r.MaxX, r.MaxY));
                _dc.DrawLine(Ink.VoidMark, P(r.MinX, r.MaxY), P(r.MaxX, r.MinY));
            }
        }

        private void PaintBeams()
        {
            double textPx = Math.Max(6.5, Px(BeamTextMm));
            // Primero las viguetas (más tenues), encima las vigas.
            foreach (BeamSegment beam in Geometry.Beams.OrderBy(b => b.Role == BeamRole.Vigueta ? 0 : 1))
            {
                Pen pen = beam.Role == BeamRole.Vigueta ? Ink.Joist : Ink.Beam;
                Rect2 box = beam.Box;
                if (beam.Along == Axis2.X)
                {
                    _dc.DrawLine(pen, P(box.MinX, box.MinY), P(box.MaxX, box.MinY));
                    _dc.DrawLine(pen, P(box.MinX, box.MaxY), P(box.MaxX, box.MaxY));
                }
                else
                {
                    _dc.DrawLine(pen, P(box.MinX, box.MinY), P(box.MinX, box.MaxY));
                    _dc.DrawLine(pen, P(box.MaxX, box.MinY), P(box.MaxX, box.MaxY));
                }
            }

            foreach (BeamSegment beam in Geometry.Beams.Where(b => b.Label is not null))
            {
                string label = Settings.BeamNaming == BeamNaming.Short ? beam.Label! : $"({beam.Section})";
                foreach (Point2 at in Geometry.LabelPoints(beam))
                {
                    DrawText(label, P(at), textPx, Ink.Text, rotate: beam.Along == Axis2.Y);
                }
            }
        }

        private void PaintColumns()
        {
            foreach (ColumnBox column in Geometry.Columns)
            {
                _dc.DrawRectangle(Ink.Column, Ink.ColumnEdge, new Rect(P(column.Box.MinX, column.Box.MaxY), P(column.Box.MaxX, column.Box.MinY)));
            }
        }

        /// <summary>Ejes punteados con burbuja en ambos extremos, por fuera de las cotas.</summary>
        private void PaintAxes()
        {
            Rect2 ext = Geometry.Extents;
            double radius = Math.Max(6, Px(BubbleRadiusMm));
            double textPx = Math.Max(7, Px(3));

            foreach (GridAxis axis in Geometry.Axes)
            {
                (Point a, Point b) = axis.Fixed == Axis2.X
                    ? (P(axis.Position, ext.MaxY + BubbleDistance(PlanSide.Top)), P(axis.Position, ext.MinY - BubbleDistance(PlanSide.Bottom)))
                    : (P(ext.MinX - BubbleDistance(PlanSide.Left), axis.Position), P(ext.MaxX + BubbleDistance(PlanSide.Right), axis.Position));

                Vector direction = b - a;
                direction.Normalize();
                _dc.DrawLine(Ink.Grid, a + direction * radius, b - direction * radius);

                foreach (Point center in new[] { a, b })
                {
                    _dc.DrawEllipse(Brushes.White, Ink.Bubble, center, radius, radius);
                    DrawText(axis.Name, center, textPx, Ink.Text, rotate: false, bold: true, halo: false);
                }
            }
        }

        /// <summary>Línea de cota, líneas de extensión, trazos diagonales y valores, en negro o en el color de su tipo.</summary>
        private void PaintChain(ChainSpec chain)
        {
            Brush brush = Ink.Chain(Settings.DimensionColor(chain.Kind));
            var line = new Pen(brush, 0.7);
            var extension = new Pen(new SolidColorBrush(((SolidColorBrush)brush).Color) { Opacity = 0.6 }, 0.5);
            var tick = new Pen(brush, 1.4);

            bool horizontal = chain.Along == Axis2.X;
            double at = SideInfo.LinePosition(Geometry.Extents, chain.Side, chain.Ring, Settings, _scene.ViewScale);
            int inward = chain.Side is PlanSide.Top or PlanSide.Left ? 1 : -1;

            List<Point> points = chain.Refs.Select(r => horizontal ? P(r.Position, at) : P(at, r.Position)).ToList();
            Vector along = horizontal ? new Vector(1, 0) : new Vector(0, -1);
            Vector toPlan = horizontal ? new Vector(0, inward) : new Vector(inward, 0);
            double overshoot = Px(1.5);
            double half = Px(Settings.LongTicks ? 1.4 : 0.8);
            Vector diagonal = new Vector(1, -1) * half;

            if (Settings.ShowReferenceLines)
            {
                PaintReferenceLines(chain, at, brush);
            }

            _dc.DrawLine(line, points[0] - along * overshoot, points[^1] + along * overshoot);
            foreach (Point p in points)
            {
                _dc.DrawLine(extension, p + toPlan * Px(2), p - toPlan * Px(1));
                _dc.DrawLine(tick, p - diagonal, p + diagonal);
            }

            double textPx = Math.Max(7.5, Px(DimTextMm));
            double gap = Px(0.8) + textPx * 0.65;
            bool raiseNext = false;
            for (int i = 0; i < chain.Refs.Count - 1; i++)
            {
                double value = chain.Refs[i + 1].Position - chain.Refs[i].Position;
                string text = value.ToString("0.00", CultureInfo.InvariantCulture);

                // Si el número no cabe en su tramo, se alterna un escalón hacia afuera para no montarse con el vecino.
                bool fits = TextWidth(text, textPx) <= (points[i + 1] - points[i]).Length * 0.95;
                double offset = gap + (!fits && raiseNext ? textPx * 1.15 : 0);
                raiseNext = !fits && !raiseNext;

                // En el medio del tramo, salvo que comparta línea con otro voladizo: entonces lado a lado con su texto.
                Point center = chain.TextAt is { } textAt && chain.SegmentCount == 1
                    ? (horizontal ? P(textAt, at) : P(at, textAt))
                    : points[i] + (points[i + 1] - points[i]) / 2;
                Point mid = center - toPlan * (offset + chain.TextRow * textPx * 1.4);
                DrawText(text, mid, textPx, brush, rotate: !horizontal);
            }
        }

        /// <summary>
        /// Línea punteada desde el borde del elemento medido hasta la línea de cota,
        /// para ver qué mide cada punto aunque el elemento esté lejos (p. ej. una viga intermedia).
        /// Los ejes no la necesitan: ya cruzan toda la planta.
        /// </summary>
        private void PaintReferenceLines(ChainSpec chain, double at, Brush brush)
        {
            var pen = new Pen(new SolidColorBrush(((SolidColorBrush)brush).Color) { Opacity = 0.55 }, 0.6)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0),
            };

            Axis2 across = chain.Along.Other();
            bool outwardIsMax = SideInfo.Outward(chain.Side) > 0;
            Dictionary<long, Rect2> boxes = Geometry.Solids.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Box);

            foreach (ChainRef reference in chain.Refs.Where(r => r.Kind != RefKind.Axis))
            {
                if (!boxes.TryGetValue(reference.ElementId, out Rect2 box))
                {
                    continue;
                }

                // Borde del elemento más cercano a la cota. Si está en el contorno de la planta, no hace falta.
                double near = outwardIsMax ? box.Max(across) : box.Min(across);
                double edge = outwardIsMax ? Geometry.Extents.Max(across) : Geometry.Extents.Min(across);
                if (Math.Abs(near - edge) < InsideThresholdM)
                {
                    continue;
                }

                (Point from, Point to) = chain.Along == Axis2.X
                    ? (P(reference.Position, near), P(reference.Position, at))
                    : (P(near, reference.Position), P(at, reference.Position));
                _dc.DrawLine(pen, from, to);
            }
        }

        // ---------- texto ----------

        private double TextWidth(string text, double size) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size, Brushes.Black, _pixelsPerDip).Width;

        /// <summary>Texto centrado en <paramref name="center"/>, con halo blanco para leerse sobre las líneas.</summary>
        private void DrawText(string text, Point center, double size, Brush brush, bool rotate, bool bold = false, bool halo = true)
        {
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                bold ? Bold : Regular, size, brush, _pixelsPerDip);
            var origin = new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2);

            if (rotate)
            {
                _dc.PushTransform(new RotateTransform(-90, center.X, center.Y));
            }

            System.Windows.Media.Geometry glyphs = formatted.BuildGeometry(origin);
            if (halo)
            {
                _dc.DrawGeometry(null, new Pen(Brushes.White, size * 0.35) { LineJoin = PenLineJoin.Round }, glyphs);
            }

            _dc.DrawGeometry(brush, null, glyphs);

            if (rotate)
            {
                _dc.Pop();
            }
        }
    }
}
