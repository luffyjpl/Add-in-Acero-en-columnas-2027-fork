using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CQEING.Zapatas.Tablero;

/// <summary>
/// La ventana del tablero de zapatas (#247): un WebView2 que muestra la página de <see cref="CarpetaWeb"/>. Se arma en
/// código y sin XAML: en Revit 2027 el lector de XAML no resuelve el WebView2 que trae Revit.
/// </summary>
internal sealed class VentanaTablero : Window
{
    /// <summary>Host virtual con el que WebView2 sirve <see cref="CarpetaWeb"/>.</summary>
    private const string Host = "zapatas.cqeing";

    private bool cerrada;

    private readonly WebView2 navegador = new()
    {
        DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xEE, 0xF0, 0xF2),
    };

    public VentanaTablero(IntPtr duenio)
    {
        Title = "Acero en zapatas";
        Width = 1100;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xF0, 0xF2));
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
                MessageBox.Show(this, $"No se pudo abrir el tablero de zapatas.\n\n{e}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        };
        Closed += (_, _) =>
        {
            cerrada = true;
            navegador.Dispose();
        };
    }

    /// <summary>La carpeta <c>web</c> junto a la DLL, donde la compilación copia la página.</summary>
    private static string CarpetaWeb { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(VentanaTablero).Assembly.Location) ?? string.Empty, "web");

    /// <summary>Datos de usuario de WebView2: la carpeta de Revit no se puede escribir.</summary>
    private static string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CQEING", "zapatas", "WebView2");

    public void Volver()
    {
        if (cerrada)
            return;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private async Task IniciarAsync()
    {
        CoreWebView2Environment entorno = await CoreWebView2Environment.CreateAsync(null, CarpetaDatos);
        await navegador.EnsureCoreWebView2Async(entorno);
        CoreWebView2 core = navegador.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(Host, CarpetaWeb, CoreWebView2HostResourceAccessKind.Deny);
        core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
#endif
        // Solo esta página: soltar un archivo o un enlace sobre la ventana no la cambia por otra.
        core.NavigationStarting += (_, e) => e.Cancel = !EsDeLaPagina(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.Navigate($"https://{Host}/index.html");
    }

    private static bool EsDeLaPagina(string? uri) => uri?.StartsWith($"https://{Host}/", StringComparison.OrdinalIgnoreCase) == true;
}
