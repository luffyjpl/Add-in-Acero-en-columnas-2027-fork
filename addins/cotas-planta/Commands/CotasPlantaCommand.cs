using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;
using CQEING.CotasPlanta.Revit;
using CQEING.CotasPlanta.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CQEING.CotasPlanta.Commands
{
    /// <summary>
    /// Botón de Revit: abre la ventana de cotas de planta. La ventana no bloquea Revit;
    /// si ya está abierta, solo la trae al frente.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class CotasPlantaCommand : IExternalCommand
    {
        private static MainWindow? _openWindow;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_openWindow is not null)
            {
                _openWindow.Activate();
                return Result.Succeeded;
            }

            UIApplication app = commandData.Application;
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc is null)
            {
                message = "Abre un proyecto antes de ejecutar el comando.";
                return Result.Failed;
            }

            IReadOnlyList<PlanEntry> plans = PlanCatalog.List(doc);
            if (plans.Count == 0)
            {
                TaskDialog.Show("Cotas de planta", "El proyecto no tiene plantas con nivel asociado.");
                return Result.Cancelled;
            }

            // Se lee todo ahora, mientras Revit lo permite: la ventana abierta ya no puede leer el modelo.
            double tolerance = new CotasSettings().OnAxisToleranceM;
            Dictionary<long, PlanGeometry> geometries = plans.ToDictionary(
                p => p.Id,
                p => PlanGeometryReader.Read((ViewPlan)doc.GetElement(new ElementId(p.Id)), tolerance));

            var request = new GenerateRequest(doc);
            var viewModel = new MainViewModel(
                doc.Title,
                plans,
                PlanCatalog.LinearDimensionTypeNames(doc),
                viewId => geometries[viewId],
                request.Raise,
                app.ActiveUIDocument!.ActiveView?.Id.Value);
            request.Generated += viewModel.MarkGenerated;

            _openWindow = new MainWindow(viewModel);
            _openWindow.SetOwner(app.MainWindowHandle);
            _openWindow.Closed += (_, _) => _openWindow = null;
            _openWindow.Show();
            return Result.Succeeded;
        }
    }
}
