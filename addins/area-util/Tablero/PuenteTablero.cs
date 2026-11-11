using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CQEING.AreaUtil.Calculo;
using CQEING.AreaUtil.Revit;

namespace CQEING.AreaUtil.Tablero;

/// <summary>
/// Une la página con Revit. Calcular y leer puntos no usan la API y se contestan enseguida; leer niveles y generar van al
/// hilo de Revit por <see cref="AccionesRevit"/>. Sigue al modelo activo como zapatas.
/// </summary>
internal sealed class PuenteTablero
{
    private readonly VentanaTablero ventana;
    private readonly UIApplication uiapp;
    private readonly AccionesRevit acciones = new();
    private Document? doc;
    /// <summary>Ya se pidió cerrar: el cierre de verdad espera a que corra en el hilo de Revit.</summary>
    private bool cerrando;
    /// <summary>Los eventos de Revit ya se quitaron.</summary>
    private bool soltado;

    public PuenteTablero(VentanaTablero ventana, UIApplication uiapp)
    {
        this.ventana = ventana;
        this.uiapp = uiapp;
        doc = uiapp.ActiveUIDocument?.Document;
        ventana.MensajeRecibido += Recibir;
        acciones.Fallo += texto => ventana.Enviar(ProtocoloAreaUtil.Aviso($"No se pudo hacer en Revit: {texto}"));
        uiapp.ViewActivated += AlActivarVista;
        uiapp.Application.DocumentClosing += AlCerrarDocumento;
        ventana.Closing += AlPedirCierre;
        ventana.Closed += (_, _) => Soltar();
    }

    /// <summary>
    /// Quitar un evento de Revit fuera de su contexto de API lanza InvalidOperationException, y en el Closed de WPF eso
    /// cerraba Revit entero (journals 0003 y 0004 del 10-nov, hallazgo 10 del plan). Por eso el cierre se cancela y se
    /// repite desde la cola de Revit, donde la API es válida.
    /// </summary>
    private void AlPedirCierre(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (soltado)
            return;
        e.Cancel = true;
        if (cerrando)
            return;
        cerrando = true;
        acciones.Encolar(_ =>
        {
            QuitarEventos();
            ventana.Close();
        });
    }

    private void Recibir(string json)
    {
        MensajeAreaUtil mensaje = ProtocoloAreaUtil.Leer(json);
        switch (mensaje)
        {
            case CalcularAreaUtil c:
                ventana.Enviar(ProtocoloAreaUtil.Resultado(CalculoLote.Calcular(c.Entrada)));
                break;
            case LeerPuntosAreaUtil l:
                var (puntos, errores) = LectorPuntos.Leer(l.Texto);
                ventana.Enviar(ProtocoloAreaUtil.Puntos(puntos, errores));
                break;
            case CerrarAreaUtil:
                ventana.Close();
                break;
            case DesconocidoAreaUtil:
                break;
            default:
                acciones.Encolar(app => Atender(app, mensaje));
                break;
        }
    }

    private void Atender(UIApplication app, MensajeAreaUtil mensaje)
    {
        if (mensaje is ListoAreaUtil)
        {
            Recargar(app);
            return;
        }
        // Se genera solo sobre el modelo leído y si sigue siendo el activo.
        if (doc is null || !doc.IsValidObject || app.ActiveUIDocument?.Document is not Document activo || !activo.Equals(doc))
        {
            Recargar(app);
            return;
        }
        if (mensaje is GenerarAreaUtil g)
            Generar(doc, g);
    }

    private void Recargar(UIApplication app)
    {
        doc = app.ActiveUIDocument?.Document;
        if (doc is null || doc.IsFamilyDocument)
        {
            doc = null;
            ventana.Enviar(ProtocoloAreaUtil.Datos("", [], null));
            ventana.Enviar(ProtocoloAreaUtil.Aviso("No hay un modelo de proyecto abierto. Abra uno para dibujar el lote."));
            return;
        }
        List<Level> niveles = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
        string? activo = (doc.ActiveView?.GenLevel ?? niveles.FirstOrDefault())?.Id.ToString();
        ventana.Enviar(ProtocoloAreaUtil.Datos(doc.Title,
            niveles.Select(l => new NivelLeido(l.Id.ToString(), l.Name, UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Meters))).ToList(),
            activo));
    }

    private void Generar(Document doc, GenerarAreaUtil g)
    {
        ResultadoLote r = CalculoLote.Calcular(g.Entrada);
        if (!r.SePuedeGenerar)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso("Hay avisos en rojo: corríjalos antes de generar."));
            return;
        }
        if (doc.GetElement(new ElementId(long.Parse(g.NivelId))) is not Level nivel)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso("El nivel elegido ya no existe. Elija otro."));
            Recargar(uiapp);
            return;
        }
        List<ElementId> creadas;
        try
        {
            creadas = GeneradorLote.Generar(doc, r, nivel, g.Compartidas, g.Estilos);
        }
        catch (InvalidOperationException e)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso(e.Message));
            return;
        }
        ventana.Enviar(ProtocoloAreaUtil.Generado(creadas.Count));
        Mostrar(doc, nivel, creadas);
    }

    /// <summary>
    /// Decisión 16: se aparta la ventana, se abre una planta del nivel si la vista activa no lo es, y se seleccionan y
    /// muestran las líneas creadas, como «Mostrar en Revit» de vigas y zapatas.
    /// </summary>
    private void Mostrar(Document doc, Level nivel, List<ElementId> creadas)
    {
        if (creadas.Count == 0 || uiapp.ActiveUIDocument is not UIDocument uidoc)
            return;
        ventana.Apartar();
        if (!(uidoc.ActiveView is ViewPlan activa && activa.GenLevel?.Id == nivel.Id) && Planta(doc, nivel) is ViewPlan planta)
            uidoc.ActiveView = planta;
        uidoc.Selection.SetElementIds(creadas);
        uidoc.ShowElements(creadas);
    }

    /// <summary>La primera planta del nivel que no sea plantilla: de piso, estructural o de techo, en ese orden.</summary>
    private static ViewPlan? Planta(Document doc, Level nivel)
    {
        ViewType[] orden = [ViewType.FloorPlan, ViewType.EngineeringPlan, ViewType.CeilingPlan];
        return new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
            .Where(v => !v.IsTemplate && v.GenLevel?.Id == nivel.Id && Array.IndexOf(orden, v.ViewType) >= 0)
            .OrderBy(v => Array.IndexOf(orden, v.ViewType)).ThenBy(v => v.Name)
            .FirstOrDefault();
    }

    private void AlActivarVista(object? sender, ViewActivatedEventArgs e)
    {
        try
        {
            if (!cerrando && !soltado && !e.Document.Equals(doc))
                acciones.Encolar(Recargar);
        }
        catch
        {
            // Revit llama este evento desde su lado nativo: un error aquí podría tumbarlo.
        }
    }

    private void AlCerrarDocumento(object? sender, DocumentClosingEventArgs e)
    {
        try
        {
            if (!cerrando && !soltado && e.Document.Equals(doc))
            {
                doc = null;
                acciones.Encolar(Recargar);
            }
        }
        catch
        {
            // Igual que arriba.
        }
    }

    /// <summary>Solo dentro del contexto de la API de Revit (un trabajo de <see cref="AccionesRevit"/>).</summary>
    private void QuitarEventos()
    {
        uiapp.ViewActivated -= AlActivarVista;
        uiapp.Application.DocumentClosing -= AlCerrarDocumento;
        soltado = true;
    }

    private void Soltar()
    {
        if (!soltado)
        {
            // La ventana se cerró sin pasar por AlPedirCierre (Revit se está cerrando y destruye sus ventanas): fuera del
            // contexto de la API quitar los eventos lanza, y Revit se va a cerrar igual.
            try
            {
                QuitarEventos();
            }
            catch
            {
                soltado = true;
            }
        }
        // Se libera después: este Closed puede estar corriendo dentro del propio ExternalEvent (el cierre encolado).
        ventana.Dispatcher.BeginInvoke(acciones.Dispose);
    }
}
