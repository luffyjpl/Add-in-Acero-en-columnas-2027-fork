using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Web.WebView2.Core;

namespace CQEING.Viguetas.Interfaz;

/// <summary>Abre «Crear viguetas» (#947), una sola ventana sin modo; si ya está abierta, la trae al frente.</summary>
[Transaction(TransactionMode.Manual)]
public sealed class ComandoViguetas : IExternalCommand
{
    private const string Titulo = "Crear viguetas";

    private const string FaltaRuntime =
        "Falta Microsoft Edge WebView2 Runtime. Instálelo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 y vuelva a abrir «Crear viguetas».";

    private static VentanaViguetas? abierta;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (abierta is not null)
        {
            abierta.Volver();
            return Result.Succeeded;
        }
        UIApplication uiapp = commandData.Application;
        if (uiapp.ActiveUIDocument?.Document is not Document doc || doc.IsFamilyDocument)
        {
            TaskDialog.Show(Titulo, "Abra un modelo de proyecto para crear viguetas.");
            return Result.Cancelled;
        }
        try
        {
            CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            TaskDialog.Show(Titulo, FaltaRuntime);
            return Result.Cancelled;
        }

        VentanaViguetas ventana;
        try
        {
            ventana = new VentanaViguetas(uiapp.MainWindowHandle);
        }
        catch (Exception e)
        {
            message = e.ToString();
            return Result.Failed;
        }
        // El ExternalEvent del puente se crea aquí, con la API válida: no se puede crear desde la ventana.
        _ = new PuenteViguetas(ventana, doc);
        ventana.Closed += (_, _) => abierta = null;
        abierta = ventana;
        ventana.Show();
        return Result.Succeeded;
    }
}
