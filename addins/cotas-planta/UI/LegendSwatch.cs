using System.Windows;
using System.Windows.Media;
using CQEING.CotasPlanta.Domain;

namespace CQEING.CotasPlanta.UI
{
    /// <summary>El símbolo pequeño de cada línea de la leyenda.</summary>
    public sealed class LegendSwatch : FrameworkElement
    {
        public static readonly DependencyProperty EntryProperty = DependencyProperty.Register(
            nameof(Entry), typeof(LegendEntry), typeof(LegendSwatch),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public LegendEntry? Entry
        {
            get => (LegendEntry?)GetValue(EntryProperty);
            set => SetValue(EntryProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = RenderSize.Width, h = RenderSize.Height;
            switch (Entry?.Symbol)
            {
                case LegendSymbol.Chain when Entry.Kind is { } kind:
                    var line = new Pen(Ink.Chain(kind), 1);
                    var tick = new Pen(Ink.Chain(kind), 1.4);
                    dc.DrawLine(line, new Point(1, h / 2), new Point(w - 1, h / 2));
                    dc.DrawLine(tick, new Point(1, h / 2 + 4), new Point(7, h / 2 - 4));
                    dc.DrawLine(tick, new Point(w - 7, h / 2 + 4), new Point(w - 1, h / 2 - 4));
                    break;

                case LegendSymbol.Column:
                    dc.DrawRectangle(Ink.Column, Ink.ColumnEdge, new Rect(w / 2 - 5, h / 2 - 5, 10, 10));
                    break;

                case LegendSymbol.Void:
                    var box = new Rect(2, 1, w - 4, h - 2);
                    dc.DrawRectangle(null, Ink.Beam, box);
                    dc.DrawLine(Ink.VoidMark, box.TopLeft, box.BottomRight);
                    dc.DrawLine(Ink.VoidMark, box.BottomLeft, box.TopRight);
                    break;
            }
        }
    }
}
