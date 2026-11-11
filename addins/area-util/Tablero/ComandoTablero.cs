using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Web.WebView2.Core;

namespace CQEING.AreaUtil.Tablero;

/// <summary>Abre el tablero del área útil, una sola ventana sin modo; si ya está abierta, la trae al frente.</summary>
[Transaction(TransactionMode.Manual)]
public sealed class ComandoTablero : IExternalCommand
{
    private const string Titulo = "Área útil del lote";

    private const string FaltaRuntime =
        "Falta Microsoft Edge WebView2 Runtime. Instálelo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 y vuelva a abrir el tablero.";

    private static VentanaTablero? abierta;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (abierta is not null)
        {
            abierta.Volver();
            return Result.Succeeded;
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

        UIApplication uiapp = commandData.Application;
        if (uiapp.ActiveUIDocument?.Document is not Document doc || doc.IsFamilyDocument)
        {
            TaskDialog.Show(Titulo, "Abra un modelo de proyecto para dibujar el lote.");
            return Result.Cancelled;
        }

        VentanaTablero ventana;
        try
        {
            ventana = new VentanaTablero(uiapp.MainWindowHandle);
        }
        catch (Exception e)
        {
            message = e.ToString();
            return Result.Failed;
        }
        // El ExternalEvent del puente se crea aquí, con la API válida: no se puede crear desde la ventana.
        _ = new PuenteTablero(ventana, uiapp);
        ventana.Closed += (_, _) => abierta = null;
        abierta = ventana;
        ventana.Show();
        return Result.Succeeded;
    }
}
