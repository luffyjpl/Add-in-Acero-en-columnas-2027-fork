namespace practicacq.Logica;

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

/// <summary>
/// Las advertencias de Revit al generar, explicadas por el add-in en lugar del
/// diálogo de Revit (decisiones 11 y 12 de
/// docs/planes/2026-11-07-estribos-centrados-y-fallas-santiago.md). Solo se
/// explica: lo generado no se toca.
///
/// <list type="bullet">
/// <item>Permisos de modelo compartido: Revit no deja guardar cambios en
/// elementos a nombre de otro usuario, ni si la copia está desactualizada.
/// Antes salía su diálogo ("No se puede obtener permiso para editar el
/// elemento…", 5 avisos, lo que vio Santiago el 07-nov) sin decir qué
/// elementos eran; con Cancelar, Revit deshacía todo. Las advertencias se
/// borran y se dice qué elementos, a nombre de quién y qué hacer. Los errores
/// no se pueden saltar: se deshace la generación y se dice por qué.</item>
/// <item>"Rebar is placed completely outside of its host": en THEVENIG 2 (08-nov)
/// eran las grapas de los nudos, dentro de la cruz que dejan las vigas que
/// cortan a la columna. Las piezas están en su sitio; se avisa por columna
/// con la causa y el arreglo.</item>
/// </list>
/// </summary>
internal sealed class AvisosDeRevit : IFailuresPreprocessor
{
    /// <summary>Advertencias: Revit deja seguir, pero esos cambios no se guardan.</summary>
    private static readonly HashSet<Guid> Advertencias = new()
    {
        BuiltInFailures.EditingFailures.TemporaryChangesTransparentPermissions.Guid,
        BuiltInFailures.EditingFailures.TemporaryChangesOutOfDate.Guid,
    };

    /// <summary>Las fallas de copia desactualizada: se arreglan recargando lo más reciente.</summary>
    private static readonly HashSet<Guid> Desactualizadas = new()
    {
        BuiltInFailures.EditingFailures.TemporaryChangesOutOfDate.Guid,
        BuiltInFailures.EditingFailures.OutOfDateElements.Guid,
        BuiltInFailures.EditingFailures.OwnElementsOutOfDate.Guid,
    };

    private static readonly HashSet<Guid> DePermisos = new()
    {
        BuiltInFailures.EditingFailures.TemporaryChangesTransparentPermissions.Guid,
        BuiltInFailures.EditingFailures.TemporaryChangesOutOfDate.Guid,
        BuiltInFailures.EditingFailures.OwnedByOther.Guid,
        BuiltInFailures.EditingFailures.OutOfDateElements.Guid,
        BuiltInFailures.EditingFailures.OwnElementsOutOfDate.Guid,
        BuiltInFailures.EditingFailures.HaveNoPermissionToEdit.Guid,
        BuiltInFailures.EditingFailures.CannotBorrowBusyContention.Guid,
    };

    private static readonly Guid FueraDelHost = BuiltInFailures.RebarFailures.OutSideOfHost.Guid;

    private readonly Document _doc;
    private readonly SortedSet<string> _elementos = new(StringComparer.CurrentCulture);
    private readonly SortedSet<string> _duenos = new(StringComparer.CurrentCulture);
    private bool _hayPermisos;
    private bool _desactualizada;
    private bool _centralOcupado;

    /// <summary>Por columna (host): cuántas piezas de cada forma quedaron "fuera" de ella.</summary>
    private readonly Dictionary<ElementId, Dictionary<string, int>> _fuera = new();

    public AvisosDeRevit(Document doc) => _doc = doc;

    /// <summary>True si hubo un error de permisos y por eso se deshizo la generación.</summary>
    public bool Deshizo { get; private set; }

    public FailureProcessingResult PreprocessFailures(FailuresAccessor accesor)
    {
        foreach (FailureMessageAccessor falla in accesor.GetFailureMessages())
        {
            Guid tipo = falla.GetFailureDefinitionId().Guid;

            if (tipo == FueraDelHost && falla.GetSeverity() == FailureSeverity.Warning)
            {
                foreach (ElementId id in falla.GetFailingElementIds()) AnotarFuera(id);
                accesor.DeleteWarning(falla);
                continue;
            }

            if (!DePermisos.Contains(tipo)) continue;

            _hayPermisos = true;
            if (Desactualizadas.Contains(tipo)) _desactualizada = true;
            if (tipo == BuiltInFailures.EditingFailures.CannotBorrowBusyContention.Guid) _centralOcupado = true;
            foreach (ElementId id in falla.GetFailingElementIds()) AnotarPermiso(id);

            if (falla.GetSeverity() == FailureSeverity.Warning && Advertencias.Contains(tipo))
                accesor.DeleteWarning(falla);
            else
                Deshizo = true;
        }

        return Deshizo ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
    }

    /// <summary>El aviso de permisos para el usuario, o null si no hubo.</summary>
    public string? AvisoPermisos()
    {
        if (!_hayPermisos) return null;

        string cuales = _elementos.Count == 0 ? "algunos elementos" : string.Join(", ", _elementos);
        string quien = _duenos.Count == 0 ? "" : $" Están a nombre de {string.Join(", ", _duenos.Select(d => $"«{d}»"))}.";

        string consecuencia = Deshizo
            ? "Por eso no se generó nada."
            : "El acero sí se generó, pero esos cambios no se van a guardar en el central (casi siempre son los colores por diámetro de esas vistas).";

        string arreglo = _centralOcupado
            ? "El archivo central estaba ocupado: vuelve a intentarlo en un momento."
            : _desactualizada
                ? "Tu copia está desactualizada: en Colaborar, «Volver a cargar más reciente» (o «Sincronizar con el central») y vuelve a generar."
                : "Quien los tenga tiene que sincronizar con el central liberando sus elementos. Si el nombre es el tuyo, son de otra copia " +
                  "local o de otra PC: ábrela y sincroniza desde ahí. Después vuelve a generar.";

        return $"Modelo compartido: Revit no deja guardar cambios en {cuales}.{quien} {consecuencia} {arreglo}";
    }

    /// <summary>Un aviso por columna con piezas "fuera" de ella (vacío si no hubo).</summary>
    public IEnumerable<string> AvisosFueraDelHost()
    {
        foreach ((ElementId host, Dictionary<string, int> porForma) in _fuera.OrderBy(p => p.Key.Value))
        {
            int total = porForma.Values.Sum();
            string piezas = string.Join(", ", porForma.OrderBy(p => p.Key, StringComparer.CurrentCulture)
                .Select(p => $"{p.Value} «{p.Key}»"));
            yield return
                $"{NombreColumna(host)}: Revit avisa que {total} pieza(s) ({piezas}) quedaron «completamente fuera de su host». " +
                "Lo más común es que en los nudos las vigas corten a la columna: le dejan un hueco donde van las grapas y, para Revit, " +
                "quedan fuera aunque estén en su sitio. Las piezas se generaron bien. Para quitar el aviso: Modificar → Unir geometría → " +
                "Cambiar orden de unión, primero la columna y después cada viga del nudo, y vuelve a generar.";
        }
    }

    private void AnotarFuera(ElementId id)
    {
        try
        {
            if (_doc.GetElement(id) is not Rebar barra) return;
            ElementId host = barra.GetHostId();
            string forma = _doc.GetElement(barra.GetShapeId())?.Name ?? "sin forma";

            if (!_fuera.TryGetValue(host, out Dictionary<string, int>? porForma))
                _fuera[host] = porForma = new Dictionary<string, int>();
            porForma[forma] = porForma.TryGetValue(forma, out int n) ? n + 1 : 1;
        }
        catch (Exception)
        {
            // Solo sirve para el texto del aviso.
        }
    }

    private string NombreColumna(ElementId host)
    {
        try
        {
            Element? columna = _doc.GetElement(host);
            string? marca = columna?.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
            return string.IsNullOrWhiteSpace(marca) ? $"Columna id {host.Value}" : $"Columna {marca} (id {host.Value})";
        }
        catch (Exception)
        {
            return $"Columna id {host.Value}";
        }
    }

    private void AnotarPermiso(ElementId id)
    {
        try
        {
            Element? elemento = _doc.GetElement(id);
            if (elemento is null) return;

            string nombre = elemento switch
            {
                View { IsTemplate: true } v => $"la plantilla de vista «{v.Name}»",
                View v => $"la vista «{v.Name}»",
                _ => $"«{elemento.Name}» ({elemento.Category?.Name ?? elemento.GetType().Name}, id {id.Value})",
            };
            _elementos.Add(nombre);

            if (_doc.IsWorkshared)
            {
                string dueno = WorksharingUtils.GetWorksharingTooltipInfo(_doc, id).Owner;
                if (!string.IsNullOrWhiteSpace(dueno)) _duenos.Add(dueno);
            }
        }
        catch (Exception)
        {
            // Solo sirve para el texto del aviso.
        }
    }
}
