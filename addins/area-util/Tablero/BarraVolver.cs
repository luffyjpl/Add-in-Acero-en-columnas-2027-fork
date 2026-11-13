using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace CQEING.AreaUtil.Tablero;

/// <summary>
/// La barrita que queda arriba del dibujo cuando la ventana se minimiza al generar (decisión 24, punto 3): un clic la
/// trae de vuelta, sin buscar el botón de la cinta. Solo interfaz: no llama a la API de Revit.
/// </summary>
internal sealed class BarraVolver : Window
{
    public BarraVolver(IntPtr duenio, (int Izquierda, int Arriba, int Derecha, int Abajo) areaDibujo, string texto)
    {
        Title = "Área útil del lote";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        ShowActivated = false;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var volver = BarraDibujo.Boton("Volver al add-in");
        volver.Click += (_, _) => Volver?.Invoke();
        var cerrar = BarraDibujo.Boton("×");
        cerrar.BorderBrush = Brushes.Transparent;
        cerrar.ToolTip = "Ocultar esta barra";
        cerrar.Click += (_, _) => Close();

        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        fila.Children.Add(volver);
        fila.Children.Add(cerrar);
        Content = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xCF, 0xD6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 6, 6, 6),
            Margin = new Thickness(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.25 },
            Child = fila,
        };

        new WindowInteropHelper(this).Owner = duenio;
        Loaded += (_, _) =>
        {
            Matrix aDip = PresentationSource.FromVisual(this)?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            Point arriba = aDip.Transform(new Point((areaDibujo.Izquierda + areaDibujo.Derecha) / 2.0, areaDibujo.Arriba));
            Left = arriba.X - ActualWidth / 2;
            Top = arriba.Y + 4;
        };
    }

    public event Action? Volver;
}
