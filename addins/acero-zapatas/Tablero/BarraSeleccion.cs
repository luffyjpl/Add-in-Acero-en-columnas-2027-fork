using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CQEING.Zapatas.Tablero;

/// <summary>
/// La barra de "Seleccionar en Revit", igual a la de vigas: va arriba del área de dibujo mientras el usuario elige
/// zapatas con la selección normal de Revit, y dice cuántas lleva. No usa <c>PickObjects</c>, así que Revit no muestra
/// su Finalizar/Cancelar ni queda bloqueado. Se arma en código, como <see cref="VentanaTablero"/>.
/// </summary>
internal sealed class BarraSeleccion : Window
{
    private static readonly Brush Azul = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xAF));
    private static readonly Brush Gris = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD));
    private static readonly Brush Tinta = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x1D));

    private readonly TextBlock conteo = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0), MinWidth = 150 };
    private readonly Button terminar;

    /// <param name="duenio">La ventana principal de Revit: la barra queda encima de ella.</param>
    /// <param name="areaDibujo">El área de dibujo de Revit en píxeles de pantalla (izquierda, arriba, derecha, abajo).</param>
    public BarraSeleccion(IntPtr duenio, (int Izquierda, int Arriba, int Derecha, int Abajo) areaDibujo)
    {
        Title = "Seleccionar zapatas";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        ShowActivated = false;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        Foreground = Tinta;

        var todas = Boton("Todas", principal: false);
        todas.Click += (_, _) => Todas?.Invoke();
        terminar = Boton("Terminar", principal: true);
        terminar.Click += (_, _) => Terminar?.Invoke();
        var cancelar = Boton("Cancelar", principal: false);
        cancelar.BorderBrush = Brushes.Transparent;
        cancelar.Click += (_, _) => Cancelar?.Invoke();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Cancelar?.Invoke();
        };

        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock { Text = "Elija zapatas en Revit", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        fila.Children.Add(conteo);
        fila.Children.Add(todas);
        fila.Children.Add(terminar);
        fila.Children.Add(cancelar);
        Content = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xCF, 0xD6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 8, 8),
            Margin = new Thickness(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.25 },
            Child = fila,
        };

        new WindowInteropHelper(this).Owner = duenio;
        Mostrar(0);
        // Centrada arriba del área de dibujo; la posición de Revit viene en píxeles y WPF trabaja en DIP.
        Loaded += (_, _) =>
        {
            Matrix aDip = PresentationSource.FromVisual(this)?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            Point arriba = aDip.Transform(new Point((areaDibujo.Izquierda + areaDibujo.Derecha) / 2.0, areaDibujo.Arriba));
            Left = arriba.X - ActualWidth / 2;
            Top = arriba.Y + 4;
        };
    }

    public event Action? Todas;

    public event Action? Terminar;

    public event Action? Cancelar;

    /// <summary>Cuántas zapatas hay elegidas; sin ninguna, "Terminar" no se puede pulsar.</summary>
    public void Mostrar(int zapatas)
    {
        conteo.Text = zapatas == 0 ? "Ninguna zapata elegida" : $"{zapatas} {(zapatas == 1 ? "zapata elegida" : "zapatas elegidas")}";
        terminar.IsEnabled = zapatas > 0;
        terminar.Background = zapatas > 0 ? Azul : Gris;
        terminar.BorderBrush = terminar.Background;
    }

    private static Button Boton(string texto, bool principal)
    {
        var plantilla = new ControlTemplate(typeof(Button));
        var borde = new FrameworkElementFactory(typeof(Border));
        borde.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borde.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borde.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        borde.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        borde.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var contenido = new FrameworkElementFactory(typeof(ContentPresenter));
        contenido.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contenido.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        borde.AppendChild(contenido);
        plantilla.VisualTree = borde;
        return new Button
        {
            Content = texto,
            Template = plantilla,
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand,
            Background = principal ? Azul : Brushes.White,
            Foreground = principal ? Brushes.White : Azul,
            BorderBrush = Azul,
            FontWeight = principal ? FontWeights.SemiBold : FontWeights.Normal,
        };
    }
}
