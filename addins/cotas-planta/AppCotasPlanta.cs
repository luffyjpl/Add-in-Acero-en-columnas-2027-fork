using Autodesk.Revit.UI;
using CQEING.Compartido;

namespace CQEING.CotasPlanta;

/// <summary>Registra el botón «Cotas de planta» en el panel «Planos» de la pestaña CQEING.</summary>
public class AppCotasPlanta : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        // Sin licencia vigente no se crea el botón; el aviso lo da el componente del gestor (issue #189).
        if (!Licencia.Vigente(out _)) return Result.Succeeded;
        // Con una versión nueva publicada, tampoco: hay que actualizar desde el gestor (actualización obligatoria).
        if (!BloqueoPorVersion.AlDia(app)) return Result.Succeeded;

        RibbonPanel panel = CintaCqeing.ObtenerPanel(app, "Planos");

        panel.AddItem(CintaCqeing.CrearBoton(
            "ComandoCotasPlanta",
            "Cotas\nde planta",
            "CQEING.CotasPlanta.Commands.CotasPlantaCommand",
            "Genera la vista «Cotas <planta>» con cotas libres, eje a eje, voladizos, viguetas y total, con vista previa.",
            "iconoCQ"));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
}
