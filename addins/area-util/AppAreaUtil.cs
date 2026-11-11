using Autodesk.Revit.UI;
using CQEING.Compartido;

namespace CQEING.AreaUtil;

/// <summary>Registra el panel «Lote» en la pestaña CQEING. Reunión del 09-nov-2026; spec 2026-11-10-area-util-lote.</summary>
public class AppAreaUtil : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        // Sin licencia vigente no se crea el botón; el aviso lo da el componente del gestor (issue #189).
        if (!Licencia.Vigente(out _)) return Result.Succeeded;
        // Con una versión nueva publicada, tampoco: hay que actualizar desde el gestor (actualización obligatoria).
        if (!BloqueoPorVersion.AlDia(app)) return Result.Succeeded;

        RibbonPanel panel = CintaCqeing.ObtenerPanel(app, "Lote");
        panel.AddItem(CintaCqeing.CrearBoton(
            "ComandoAreaUtil",
            "Área útil del lote",
            "CQEING.AreaUtil.Tablero.ComandoTablero",
            "Dibuja el lote, los retiros de la curaduría y el área útil del primer piso.",
            "iconoCQ"));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
}
