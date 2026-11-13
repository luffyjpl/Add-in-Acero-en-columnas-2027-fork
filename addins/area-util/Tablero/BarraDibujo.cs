using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CQEING.AreaUtil.Tablero;

/// <summary>Lo que se pidió desde la barra mientras Revit esperaba un clic.</summary>
internal enum AccionBarra
{
    Ninguna,
    Deshacer,
    Cancelar,
}

/// <summary>
/// La barra del dibujo libre (decisión 21 del plan), arriba del área de dibujo, como la de zapatas. Mientras Revit
/// espera un clic (<c>PickPoint</c>) sus botones no pueden llamar a la API: guardan la acción y le mandan Esc a Revit
/// para cortar esa espera; el ciclo del dibujo la toma al recibir la cancelación. Primero con <c>PostMessage</c> y, si
/// en 400 ms el ciclo no la tomó, con el teclado (decisión 22: se mide cuál sirve).
/// </summary>
internal sealed class BarraDibujo : Window
{
    private const uint WmKeyDown = 0x0100, WmKeyUp = 0x0101, TeclaArriba = 0x0002;
    private const byte Escape = 0x1B;

    private static readonly Brush Azul = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xAF));
    private static readonly Brush Tinta = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x1D));

    private readonly IntPtr revit;
    private readonly Action<string> anotar;
    private readonly TextBlock conteo = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0), MinWidth = 120 };
    private readonly Button deshacer;
    private AccionBarra pendiente = AccionBarra.Ninguna;

    /// <param name="duenio">La ventana principal de Revit: la barra queda encima de ella.</param>
    /// <param name="areaDibujo">El área de dibujo de Revit en píxeles de pantalla.</param>
    /// <param name="anotar">Dónde se anota lo que pasa (el diagnóstico); en uso normal no anota nada.</param>
    public BarraDibujo(IntPtr duenio, (int Izquierda, int Arriba, int Derecha, int Abajo) areaDibujo, Action<string> anotar)
    {
        revit = duenio;
        this.anotar = anotar;
        Title = "Dibujar el lote";
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

        deshacer = Boton("Deshacer punto");
        deshacer.Click += (_, _) => Pedir(AccionBarra.Deshacer);
        var cancelar = Boton("Cancelar");
        cancelar.BorderBrush = Brushes.Transparent;
        cancelar.Click += (_, _) => Pedir(AccionBarra.Cancelar);

        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(new TextBlock
        {
            Text = "Clic en cada esquina del lote · Esc o clic en el primer punto para terminar",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        });
        fila.Children.Add(conteo);
        fila.Children.Add(deshacer);
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

    public void Mostrar(int puntos)
    {
        conteo.Text = puntos == 0 ? "Sin puntos" : $"{puntos} {(puntos == 1 ? "punto" : "puntos")}";
        deshacer.IsEnabled = puntos > 0;
    }

    /// <summary>La acción que se pidió desde la barra, una sola vez: el ciclo del dibujo la toma al recibir el Esc.</summary>
    public AccionBarra Tomar()
    {
        AccionBarra a = pendiente;
        pendiente = AccionBarra.Ninguna;
        return a;
    }

    private void Pedir(AccionBarra accion)
    {
        pendiente = accion;
        anotar($"Barra: clic en {accion}; se manda Esc a Revit con PostMessage");
        PostMessage(revit, WmKeyDown, (IntPtr)Escape, IntPtr.Zero);
        PostMessage(revit, WmKeyUp, (IntPtr)Escape, IntPtr.Zero);
        var espera = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        espera.Tick += (_, _) =>
        {
            espera.Stop();
            if (pendiente != accion)
                return;
            anotar($"Barra: PostMessage no cortó el clic en 400 ms; se manda Esc con el teclado");
            SetForegroundWindow(revit);
            keybd_event(Escape, 0, 0, UIntPtr.Zero);
            keybd_event(Escape, 0, TeclaArriba, UIntPtr.Zero);
        };
        espera.Start();
    }

    internal static Button Boton(string texto)
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
            Background = Brushes.White,
            Foreground = Azul,
            BorderBrush = Azul,
        };
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr ventana, uint mensaje, IntPtr w, IntPtr l);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr ventana);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte tecla, byte codigo, uint banderas, UIntPtr extra);
}
