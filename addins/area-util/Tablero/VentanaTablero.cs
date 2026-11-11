using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CQEING.AreaUtil.Tablero;

/// <summary>
/// La ventana del tablero del área útil: un WebView2 que muestra la página de <see cref="CarpetaWeb"/> y habla con C# por
/// mensajes JSON (<see cref="MensajeRecibido"/> y <see cref="Enviar"/>). Se arma en código y sin XAML: en Revit 2027 el
/// lector de XAML no resuelve el WebView2 que trae Revit.
/// </summary>
internal sealed class VentanaTablero : Window
{
    /// <summary>Host virtual con el que WebView2 sirve <see cref="CarpetaWeb"/>.</summary>
    private const string Host = "area-util.cqeing";

    private bool cerrada;

    private readonly WebView2 navegador = new()
    {
        DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xDD, 0xE1, 0xE6),
    };

    public VentanaTablero(IntPtr duenio)
    {
        Rect area = SystemParameters.WorkArea;
        Title = "Área útil del lote";
        Width = Math.Min(1440, area.Width);
        Height = Math.Min(900, area.Height);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xE1, 0xE6));
        Content = navegador;
        new WindowInteropHelper(this).Owner = duenio;
        Loaded += async (_, _) =>
        {
            try
            {
                await IniciarAsync();
            }
            catch (Exception e)
            {
                // Sin esto la excepción se pierde y la ventana queda en blanco.
                MessageBox.Show(this, $"No se pudo abrir el tablero del área útil.\n\n{e}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        };
        Closed += (_, _) =>
        {
            cerrada = true;
            navegador.Dispose();
        };
    }

    /// <summary>Un mensaje de la página, como JSON (lo que la página pasó a <c>chrome.webview.postMessage</c>).</summary>
    public event Action<string>? MensajeRecibido;

    /// <summary>La carpeta <c>web</c> junto a la DLL, donde la compilación copia la página.</summary>
    private static string CarpetaWeb { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(VentanaTablero).Assembly.Location) ?? string.Empty, "web");

    /// <summary>Datos de usuario de WebView2: la carpeta de Revit no se puede escribir.</summary>
    private static string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CQEING", "area-util", "WebView2");

    private WindowState estadoAntes = WindowState.Normal;

    /// <summary>
    /// Se minimiza para dejar ver lo generado: la ventana pertenece a Revit y siempre queda encima. <see cref="Volver"/>
    /// la deja como estaba. Como vigas y zapatas (decisión 16 del plan).
    /// </summary>
    public void Apartar()
    {
        if (cerrada || WindowState == WindowState.Minimized)
            return;
        estadoAntes = WindowState;
        WindowState = WindowState.Minimized;
    }

    /// <summary>La trae al frente cuando se pulsa otra vez el botón de la cinta.</summary>
    public void Volver()
    {
        if (cerrada)
            return;
        if (WindowState == WindowState.Minimized)
            WindowState = estadoAntes;
        Activate();
    }

    /// <summary>Manda <paramref name="json"/> a la página. Después de cerrar no hace nada: puede quedar un trabajo en cola.</summary>
    public void Enviar(string json)
    {
        if (cerrada)
            return;
        // Los trabajos de Revit corren en su hilo, que es el de la ventana; el resto ya viene de la página.
        Dispatcher.Invoke(() => navegador.CoreWebView2?.PostWebMessageAsJson(json));
    }

    private async Task IniciarAsync()
    {
        CoreWebView2Environment entorno = await CoreWebView2Environment.CreateAsync(null, CarpetaDatos);
        await navegador.EnsureCoreWebView2Async(entorno);
        CoreWebView2 core = navegador.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(Host, CarpetaWeb, CoreWebView2HostResourceAccessKind.Deny);
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsPinchZoomEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
#endif
        // Solo esta página: soltar un archivo o un enlace sobre la ventana no la cambia por otra, y nada de afuera puede
        // mandarle mensajes a C#.
        core.NavigationStarting += (_, e) => e.Cancel = !EsDeLaPagina(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += (_, e) =>
        {
            if (EsDeLaPagina(e.Source))
                MensajeRecibido?.Invoke(e.WebMessageAsJson);
        };
        core.Navigate($"https://{Host}/index.html");
    }

    private static bool EsDeLaPagina(string? uri) => uri?.StartsWith($"https://{Host}/", StringComparison.OrdinalIgnoreCase) == true;
}
