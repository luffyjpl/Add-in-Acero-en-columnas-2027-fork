using Autodesk.Revit.UI;
using CQEING.Compartido;

namespace CQEING.Viguetas;

/// <summary>Registra el panel «Viguetas» en la pestaña CQEING (#947). Es un add-in propio, con su propia versión en el gestor.</summary>
public class AppViguetas : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        // Sin licencia vigente no se crea el botón; el aviso lo da el componente del gestor (issue #189).
        if (!Licencia.Vigente(out _)) return Result.Succeeded;
        // Con una versión nueva publicada, tampoco: hay que actualizar desde el gestor (actualización obligatoria).
        if (!BloqueoPorVersion.AlDia(app)) return Result.Succeeded;

        RibbonPanel panel = CintaCqeing.ObtenerPanel(app, "Viguetas");

        panel.AddItem(CintaCqeing.CrearBoton(
            "ComandoViguetas",
            "Crear viguetas",
            "CQEING.Viguetas.Interfaz.ComandoViguetas",
            "Muestra la planta del piso, reparte las viguetas del perímetro o los paneles elegidos con la fórmula del Excel y las crea en Revit.",
            "iconoCQ"));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
}
