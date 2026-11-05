using Autodesk.Revit.UI;
using CQEING.Compartido;

namespace CQEING.Zapatas;

/// <summary>Registra el panel «Zapatas» en la pestaña CQEING. Épica #246.</summary>
public class AppZapatas : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        // Sin licencia vigente no se crea el botón; el aviso lo da el componente del gestor (issue #189).
        if (!Licencia.Vigente(out _)) return Result.Succeeded;
        // Con una versión nueva publicada, tampoco: hay que actualizar desde el gestor (actualización obligatoria).
        if (!BloqueoPorVersion.AlDia(app)) return Result.Succeeded;

        RibbonPanel panel = CintaCqeing.ObtenerPanel(app, "Zapatas");

        panel.AddItem(CintaCqeing.CrearBoton(
            "ComandoTablero",
            "Acero en zapatas",
            "CQEING.Zapatas.Tablero.ComandoTablero",
            "Abre el tablero de acero en zapatas.",
            "iconoCQ"));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
}
