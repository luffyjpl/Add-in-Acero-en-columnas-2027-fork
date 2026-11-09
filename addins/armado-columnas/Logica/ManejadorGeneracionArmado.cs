namespace practicacq.Logica;

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using practicacq.Modelo;

/// <summary>
/// Ejecuta la generación de armado (J3) dentro de una transacción, en
/// contexto de API válido -- la ventana es modeless y no puede abrir
/// transacciones por su cuenta. Mismo patrón que <see cref="ManejadorGeneracion"/>
/// (columnetas/viguetas) y <see cref="ManejadorSeleccionElemento"/> (#24).
/// </summary>
public class ManejadorGeneracionArmado : IExternalEventHandler
{
    public OpcionesArmado? Opciones { get; set; }
    public Action<ResultadoArmado>? AlTerminar { get; set; }
    public Action<string>? AlFallar { get; set; }

    /// <summary>El proyecto del tablero: solo se genera si es el activo (ver <see cref="DocumentoActivo"/>).</summary>
    public Document? Documento { get; set; }

    public void Execute(UIApplication app)
    {
        if (Opciones is null) return;

        if (DocumentoActivo.DelTablero(app, Documento, out string? otroProyecto) is not Document doc)
        {
            AlFallar?.Invoke(otroProyecto ?? "No hay ningún proyecto activo en Revit.");
            return;
        }

        try
        {
            using var t = new Transaction(doc, "Generar armado de columnas");
            // Decisiones 11 y 12 del plan del 07-nov: las advertencias de
            // permisos (modelo compartido) y de piezas "fuera de su host", con
            // un aviso claro del add-in en vez del diálogo de Revit.
            var avisosRevit = new AvisosDeRevit(doc);
            FailureHandlingOptions manejo = t.GetFailureHandlingOptions();
            manejo.SetFailuresPreprocessor(avisosRevit);
            t.SetFailureHandlingOptions(manejo);
            t.Start();

            // Estribos primero: es quien borra el acero anterior (#145),
            // así que cualquier longitudinal/menor de una corrida vieja
            // (misma marca, mismo esquema) ya queda limpio antes de que J4
            // cree lo nuevo. También es quien llena
            // res.PosicionesEstribosPorTramo (issue #32) -- J4 depende de que
            // esto ya corrió para saber dónde poner los estribos menores.
            ResultadoArmado res = GeneradorArmado.Generar(doc, Opciones);
            GeneradorLongitudinal.Generar(doc, Opciones, res);

            // Colores por diámetro en las vistas donde se ven las columnas
            // (docs/planes/2026-10-20-colores-por-diametro.md).
            var columnas = Opciones.Plan.Columnas
                .SelectMany(c => c.Contexto?.Tramos ?? new List<Tramo>())
                .Select(tr => long.TryParse(tr.IdElementoHost, out long id) ? new ElementId(id) : null)
                .OfType<ElementId>()
                .ToHashSet();
            int vistas = ColoresEnVistas.Aplicar(doc, columnas, res.Avisos);
            res.Detalles.Add($"Colores por diámetro aplicados en {vistas} vistas.");

            // Decisiones 8 y 10 del plan del 07-nov: la vista 3D del add-in
            // oculta la armadura para la vista previa; después de generar se
            // vuelve a mostrar. Las demás vistas 3D no se tocan.
            Vista3DColumna.MostrarArmadura(doc);

            // #180: con qué se generó cada columna, para mostrarlo al volver a
            // elegirla. Solo lo que se creó de verdad (decisión 7).
            res.PlanGenerado = res.SoloLoGenerado(Opciones.Plan);
            if (res.PlanGenerado.Columnas.Count > 0 && RegistroGeneradosRevit.Registrar(doc, res.PlanGenerado) is string motivo)
                res.Avisos.Add($"No se pudo guardar en el modelo con qué se generó cada columna ({motivo}); el acero sí se generó.");

            // Revit puede deshacer al confirmar (un error, o Cancelar en su
            // diálogo de avisos): entonces no quedó nada, y nada se marca
            // como generado.
            TransactionStatus estado = t.Commit();
            string? avisoPermisos = avisosRevit.AvisoPermisos();
            if (estado != TransactionStatus.Committed)
            {
                AlFallar?.Invoke(avisoPermisos ?? "Revit deshizo la generación al confirmarla: no se generó nada.");
                return;
            }

            if (avisoPermisos is not null) res.Avisos.Add(avisoPermisos);
            res.Avisos.AddRange(avisosRevit.AvisosFueraDelHost());
            AlTerminar?.Invoke(res);
        }
        catch (Exception ex)
        {
            AlFallar?.Invoke(ex.Message);
        }
    }

    public string GetName() => "Generación de armado de columnas";
}
