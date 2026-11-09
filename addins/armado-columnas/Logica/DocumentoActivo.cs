namespace practicacq.Logica;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

/// <summary>
/// El tablero de columnas no tiene modo: mientras está abierto el usuario
/// puede pasar a otro proyecto, y lo que pide (generar, guardar
/// configuraciones) llega a Revit con ese otro proyecto activo. Antes se
/// generaba sobre el activo, con los ids del proyecto del tablero: cada tramo
/// daba "el elemento que lo hosteaba ya no existe en el modelo" (lo que vio
/// Santiago con varios proyectos abiertos). Decisión 6 de
/// docs/planes/2026-11-07-estribos-centrados-y-fallas-santiago.md: solo se
/// trabaja si el activo es el del tablero.
/// </summary>
public static class DocumentoActivo
{
    /// <summary>
    /// El proyecto del tablero si es el activo; si no, null y en
    /// <paramref name="motivo"/> qué hacer. Sin <paramref name="delTablero"/>
    /// (nadie lo asignó) se usa el activo, como antes.
    /// </summary>
    public static Document? DelTablero(UIApplication app, Document? delTablero, out string? motivo)
    {
        motivo = null;
        Document? activo = app.ActiveUIDocument?.Document;
        if (delTablero is null)
        {
            if (activo is null) motivo = "No hay ningún proyecto activo en Revit.";
            return activo;
        }

        if (!delTablero.IsValidObject)
        {
            motivo = "El proyecto de esta ventana ya se cerró. Cierra la ventana y vuelve a abrirla desde el proyecto.";
            return null;
        }

        if (activo is null || !activo.Equals(delTablero))
        {
            motivo = $"Esta ventana es del proyecto «{delTablero.Title}», pero en Revit está activo " +
                     $"«{activo?.Title ?? "ninguno"}». Activa «{delTablero.Title}» y vuelve a intentarlo: no se cambió nada.";
            return null;
        }

        return delTablero;
    }
}
