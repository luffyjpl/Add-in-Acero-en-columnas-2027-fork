using System;
using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>
    /// La ventana no bloquea Revit, así que no puede escribir en el modelo directamente:
    /// pide la generación con un ExternalEvent y Revit la ejecuta en su turno (forma soportada por la API).
    /// </summary>
    internal sealed class GenerateRequest : IExternalEventHandler
    {
        private readonly Document _doc;
        private readonly ExternalEvent _event;
        private (long ViewId, CotasSettings Settings)? _pending;

        public GenerateRequest(Document doc)
        {
            _doc = doc;
            _event = ExternalEvent.Create(this);
        }

        /// <summary>Pide generar la vista de cotas de una planta. Se ejecuta cuando Revit esté libre.</summary>
        public void Raise(long viewId, CotasSettings settings)
        {
            _pending = (viewId, settings);
            _event.Raise();
        }

        public void Execute(UIApplication app)
        {
            if (_pending is not { } request)
            {
                return;
            }

            _pending = null;
            if (!_doc.IsValidObject)
            {
                TaskDialog.Show("Cotas de planta", "El proyecto se cerró. Vuelve a abrir la ventana.");
                return;
            }

            if (_doc.GetElement(new ElementId(request.ViewId)) is not ViewPlan plan)
            {
                TaskDialog.Show("Cotas de planta", "La planta ya no existe en el proyecto.");
                return;
            }

            try
            {
                GenerationResult result = PlanViewGenerator.Generate(plan, request.Settings);
                if (!result.AlreadyExisted && app.ActiveUIDocument?.Document == _doc)
                {
                    app.ActiveUIDocument.ActiveView = (View)_doc.GetElement(new ElementId(result.ViewId));
                }

                TaskDialog.Show("Cotas de planta", Summary(result));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Cotas de planta", $"No se pudo generar {plan.Name}: {ex.Message}");
            }
        }

        public string GetName() => "CQEING · Cotas de planta";

        private static string Summary(GenerationResult result)
        {
            if (result.AlreadyExisted)
            {
                return $"{result.ViewName} ya existía y no se modificó. Bórrala para regenerarla.";
            }

            IEnumerable<string> lines = new[] { $"{result.ViewName}: {result.Dimensions} cotas creadas." }
                .Concat(result.Problems.Select(p => "  – " + p));
            return string.Join("\n", lines);
        }
    }
}
