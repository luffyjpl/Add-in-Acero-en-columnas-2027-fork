using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CQEING.Vigas.Calculo.Generacion;
using System.IO;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Vigas.Calculo.Tablero;
using CQEING.Zapatas.Calculo;
using CQEING.Zapatas.Revit;

namespace CQEING.Zapatas.Tablero;

/// <summary>
/// Une la página del tablero con Revit (#252). Calcular no usa la API y se contesta enseguida; leer, seleccionar y generar
/// van al hilo de Revit por <see cref="AccionesRevit"/>. Sigue al modelo activo como vigas: al cambiar de modelo o cerrarlo,
/// el tablero se recarga (decisión 33 del plan docs/planes/2026-11-05-correcciones-zapatas-reunion.md).
/// </summary>
internal sealed class PuenteTablero
{
    private readonly VentanaTablero ventana;
    private readonly UIApplication uiapp;
    private readonly AccionesRevit acciones = new();
    private Document? doc;
    /// <summary>Las reglas compartidas con vigas (decisión 38): se leen al recargar y al guardarlas.</summary>
    private readonly AlmacenReglas almacen = new(AlmacenReglas.RutaPorDefecto);
    private ReglasCargadas cargadas = new(Reglas.Generales, null);
    private Reglas reglas => cargadas.Reglas;
    private List<ZapataLeida> zapatas = [];
    private CatalogoZapatas catalogo = new([], [], []);
    private BarraSeleccion? barra;

    public PuenteTablero(VentanaTablero ventana, UIApplication uiapp)
    {
        this.ventana = ventana;
        this.uiapp = uiapp;
        doc = uiapp.ActiveUIDocument?.Document;
        ventana.MensajeRecibido += Recibir;
        acciones.Fallo += texto => ventana.Enviar(ProtocoloZapatas.Aviso($"No se pudo hacer en Revit: {texto}"));
        uiapp.ViewActivated += AlActivarVista;
        uiapp.Application.DocumentClosing += AlCerrarDocumento;
        ventana.Closed += (_, _) => Soltar();
    }

    private void Recibir(string json)
    {
        MensajeZapatas mensaje = ProtocoloZapatas.Leer(json);
        switch (mensaje)
        {
            case CalcularZapatas c:
                Calcular(c);
                break;
            case CerrarZapatas:
                ventana.Close();
                break;
            case DesconocidoZapatas:
                break;
            default:
                acciones.Encolar(app => Atender(app, mensaje));
                break;
        }
    }

    private void Atender(UIApplication app, MensajeZapatas mensaje)
    {
        if (mensaje is ListoZapatas)
        {
            Recargar(app);
            return;
        }
        if (mensaje is GuardarReglasZapatas g)
        {
            GuardarReglas(app, g.Reglas);
            return;
        }
        // Lo que toca el modelo se hace solo sobre el leído, y solo si sigue siendo el activo: si no, se mezclarían
        // elementos de un modelo con la vista o la selección de otro.
        if (doc is null || !doc.IsValidObject || app.ActiveUIDocument?.Document is not Document activo || !activo.Equals(doc))
        {
            Recargar(app);
            return;
        }
        switch (mensaje)
        {
            case SeleccionarEnRevit:
                Seleccionar(app, doc);
                break;
            case MostrarEnRevit m:
                Mostrar(app, doc, m.Ids);
                break;
            case AplicarZapatas a:
                Aplicar(app, doc, a);
                break;
            case MarcarTerminadas m:
                Marcar(doc, m);
                break;
        }
    }

    /// <summary>La palomita del tablero: pone o quita la marca en las zapatas, en una sola operación de Revit.</summary>
    private void Marcar(Document doc, MarcarTerminadas m)
    {
        using (var t = new Transaction(doc, m.Terminada ? "Marcar zapatas terminadas" : "Desmarcar zapatas terminadas"))
        {
            t.Start();
            foreach (string id in m.Ids)
                if (doc.GetElement(Id(id)) is { } zapata)
                    MarcaTerminada.Marcar(zapata, m.Terminada);
            t.Commit();
        }
        Leer(doc);
    }

    /// <summary>Lee el modelo activo; sin modelo de proyecto, el tablero queda vacío y lo dice.</summary>
    private void Recargar(UIApplication app)
    {
        doc = app.ActiveUIDocument?.Document;
        if (doc is null || doc.IsFamilyDocument)
        {
            doc = null;
            zapatas = [];
            catalogo = new([], [], []);
            ventana.Enviar(ProtocoloZapatas.Datos("", zapatas, [], catalogo, new Dictionary<string, (int, int)>()));
            ventana.Enviar(ProtocoloZapatas.Aviso("No hay un modelo de proyecto abierto. Abra uno para armar las zapatas."));
            return;
        }
        Leer(doc);
    }

    private void Leer(Document doc)
    {
        cargadas = almacen.Cargar();
        zapatas = LectorZapatas.Leer(doc);
        catalogo = TiposZapata.Catalogo(doc, reglas);
        var existentes = zapatas.ToDictionary(z => z.Id, z => doc.GetElement(Id(z.Id)) is { } e ? MarcaZapata.Contar(e) : (0, 0));
        HashSet<string> terminadas = zapatas.Where(z => MarcaTerminada.Tiene(doc.GetElement(Id(z.Id)))).Select(z => z.Id).ToHashSet();
        ventana.Enviar(ProtocoloZapatas.Datos(doc.Title, zapatas, Grupos(zapatas), catalogo, existentes, terminadas, cargadas));
        if (cargadas.Problema is { } problema)
            ventana.Enviar(ProtocoloZapatas.Aviso(problema));
        if (zapatas.Count == 0)
            ventana.Enviar(ProtocoloZapatas.Aviso("El modelo no tiene zapatas que puedan llevar acero (categoría Cimentación estructural)."));
    }

    private static IReadOnlyList<IReadOnlyList<string>> Grupos(IEnumerable<ZapataLeida> lista) =>
        AgrupadorZapatas.Agrupar(lista.Select(z => new ZapataParaAgrupar(z.Id, z.Contorno, z.Altura)));

    /// <summary>La configuración se calcula sobre la primera: las zapatas de un grupo son iguales.</summary>
    private void Calcular(CalcularZapatas c)
    {
        List<ZapataLeida> elegidas = Elegidas(c.Ids);
        if (elegidas.Count == 0)
            return;
        var avisos = new List<string>();
        var errores = new List<string>();
        if (Grupos(elegidas).Count > 1)
            errores.Add("Las zapatas elegidas no son iguales: la misma configuración no se puede aplicar a todas. Elija un solo grupo.");
        (ResultadoZapata r, IReadOnlyList<Trazo> trazos, IReadOnlyList<ChoqueZapata> choques) = Previa(elegidas[0], c.Config, c.Tipos);
        avisos.AddRange(r.Avisos);
        errores.AddRange(r.Errores);
        foreach ((string nombre, ConfigDireccion d) in new[] { ("X", c.Config.X), ("Y", c.Config.Y) })
            if (Grados(d.Terminacion) is int grados && ReglasZapata.AvisoTabla(reglas, d.Numero, grados, catalogo.Barras.FirstOrDefault(b => b.Numero == d.Numero)?.Doblado) is { } aviso)
                avisos.Add($"Dirección {nombre}: {aviso}");
        ventana.Enviar(ProtocoloZapatas.Resultado(r, c.Ids, avisos, errores, trazos, choques));
    }

    /// <summary>
    /// Lo que se verá y se creará, con lo elegido en el catálogo (paso 6 del plan de 2026-11-05): el cálculo, las barras
    /// como trazos y dónde chocan. La misma cuenta para la vista previa y para decidir si «Aplicar» pregunta.
    /// </summary>
    private (ResultadoZapata R, IReadOnlyList<Trazo> Trazos, IReadOnlyList<ChoqueZapata> Choques) Previa(ZapataLeida z, ConfigParrillas config, TiposElegidos tipos)
    {
        ConfigParrillas conTipos = ProtocoloZapatas.ConTipos(config, tipos, catalogo);
        ResultadoZapata r = MotorParrillas.Calcular(z.Entrada, conTipos, reglas);
        IReadOnlyList<Trazo> trazos = TrazosZapata.Trazos(r.Conjuntos, reglas, conTipos.Doblados, conTipos.DobladosEstribo);
        return (r, trazos, TrazosZapata.Choques(r.Conjuntos, trazos));
    }

    /// <summary>
    /// «Guardar y bloquear»: guarda en el archivo de vigas y vuelve a leer el modelo, para que el catálogo, la vista previa
    /// y lo que se genere usen las reglas nuevas (decisión 38). Como <c>SesionTablero.Guardar</c> de vigas.
    /// </summary>
    private void GuardarReglas(UIApplication app, VistaReglas pantalla)
    {
        (Reglas? nuevas, string? error) = ProtocoloZapatas.ReglasAGuardar(pantalla, reglas);
        if (nuevas is null)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso($"No se guardaron las reglas: {error}"));
            return;
        }
        try
        {
            almacen.Guardar(nuevas);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso($"No se pudieron guardar las reglas: {e.Message}"));
            return;
        }
        Recargar(app);
        ventana.Enviar(ProtocoloZapatas.Aviso("Reglas guardadas para vigas y zapatas en este PC.", bien: true));
    }

    private static int? Grados(Terminacion t) => t switch
    {
        Terminacion.Pata90 => 90,
        Terminacion.Gancho135 => 135,
        Terminacion.Gancho180 => 180,
        _ => null,
    };

    /// <summary>
    /// "Seleccionar en Revit", como en vigas: el tablero se aparta y la barra del add-in queda arriba del dibujo mientras
    /// el usuario elige con la selección normal de Revit. No se usa <c>PickObjects</c>: dejaba a Revit esperando con el
    /// tablero encima, y las demás acciones (elegir en la planta, generar) quedaban en cola sin correr.
    /// </summary>
    private void Seleccionar(UIApplication app, Document doc)
    {
        if (barra is not null)
        {
            barra.Activate();
            return;
        }
        ventana.Apartar();
        Rectangle area = app.DrawingAreaExtents;
        var nueva = new BarraSeleccion(app.MainWindowHandle, (area.Left, area.Top, area.Right, area.Bottom));
        nueva.Todas += () => acciones.Encolar(x =>
        {
            List<ElementId> todas = zapatas.Select(z => Id(z.Id)).Where(i => doc.GetElement(i) is not null).ToList();
            x.ActiveUIDocument?.Selection.SetElementIds(todas);
        });
        nueva.Terminar += () => acciones.Encolar(x =>
        {
            List<string> ids = ZapatasElegidas(x.ActiveUIDocument?.Selection.GetElementIds().Select(i => i.ToString()) ?? []);
            barra?.Close();
            ventana.Enviar(ProtocoloZapatas.Seleccion(ids));
        });
        nueva.Cancelar += () => barra?.Close();
        // Se cierre como se cierre (Terminar, Cancelar, Esc o el tablero), se deja de escuchar y el tablero vuelve.
        nueva.Closed += (_, _) =>
        {
            if (barra != nueva)
                return;
            barra = null;
            app.SelectionChanged -= AlCambiarSeleccion;
            ventana.Volver();
        };
        ventana.Closed += (_, _) =>
        {
            if (barra == nueva)
                nueva.Close();
        };
        barra = nueva;
        app.SelectionChanged += AlCambiarSeleccion;
        nueva.Mostrar(ZapatasElegidas(app.ActiveUIDocument?.Selection.GetElementIds().Select(i => i.ToString()) ?? []).Count);
        nueva.Show();
    }

    /// <summary>Lo que se elige en Revit con la barra abierta: solo se cuenta. Nunca deja escapar una excepción hacia Revit.</summary>
    private void AlCambiarSeleccion(object? sender, Autodesk.Revit.UI.Events.SelectionChangedEventArgs e)
    {
        try
        {
            if (e.GetDocument().Equals(doc))
                barra?.Mostrar(ZapatasElegidas(e.GetSelectedElements().Select(i => i.ToString())).Count);
        }
        catch
        {
            // Revit llama este evento desde su lado nativo: un error aquí podría tumbarlo.
        }
    }

    private List<string> ZapatasElegidas(IEnumerable<string> ids)
    {
        HashSet<string> conocidas = zapatas.Select(z => z.Id).ToHashSet();
        return ids.Where(conocidas.Contains).ToList();
    }

    /// <summary>"Mostrar en Revit": el tablero se aparta para que se vea; vuelve con el botón del add-in en la cinta.</summary>
    private void Mostrar(UIApplication app, Document doc, IReadOnlyList<string> ids)
    {
        List<ElementId> elementos = ids.Select(Id).Where(i => doc.GetElement(i) is not null).ToList();
        if (elementos.Count == 0)
            return;
        ventana.Apartar();
        app.ActiveUIDocument.Selection.SetElementIds(elementos);
        app.ActiveUIDocument.ShowElements(elementos);
    }

    private void Aplicar(UIApplication app, Document doc, AplicarZapatas a)
    {
        List<ZapataLeida> elegidas = Elegidas(a.Ids);
        if (elegidas.Count == 0)
            return;
        if (Grupos(elegidas).Count > 1)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso("Las zapatas elegidas no son iguales: no se les puede aplicar la misma configuración."));
            return;
        }
        // Con choques no se genera sin preguntar: la página ofrece «Revisar» o «Aplicar de todos modos» (decisión 35).
        if (!a.Forzar && Previa(elegidas[0], a.Config, a.Tipos).Choques.Count is > 0 and int choques)
        {
            ventana.Enviar(ProtocoloZapatas.Choques(choques));
            return;
        }
        ResumenGeneracion r = GeneradorZapatas.Generar(doc, elegidas, a.Config, a.Tipos, reglas, app.ActiveUIDocument.ActiveView);
        Leer(doc);
        if (r.Hecho)
        {
            string resto = r.Fallas.Count > 0 ? $" Revise: {string.Join(" ", r.Fallas)}" : " Ctrl+Z en Revit lo deshace.";
            ventana.Enviar(ProtocoloZapatas.Aviso($"Acero creado en {r.Zapatas} {(r.Zapatas == 1 ? "zapata" : "zapatas")}: {r.Barras} barras en {r.Conjuntos} conjuntos.{resto}", bien: r.Fallas.Count == 0));
        }
        else
        {
            ventana.Enviar(ProtocoloZapatas.Aviso($"No se creó nada; el modelo quedó como estaba. {string.Join(" ", r.Fallas)}"));
        }
    }

    // Los manejadores de eventos de Revit nunca dejan escapar una excepción: Revit los llama desde su lado nativo y
    // podría caerse entero. Solo encolan; el trabajo corre en AccionesRevit, que ya atrapa los errores. Igual que vigas.

    /// <summary>Otro modelo pasó a ser el activo: el tablero muestra sus zapatas.</summary>
    private void AlActivarVista(object? sender, ViewActivatedEventArgs e)
    {
        try
        {
            if (!e.Document.Equals(doc))
                acciones.Encolar(Recargar);
        }
        catch
        {
            // ver arriba
        }
    }

    /// <summary>Se cierra el modelo leído: al terminar, el tablero lee el que quede activo (o ninguno).</summary>
    private void AlCerrarDocumento(object? sender, DocumentClosingEventArgs e)
    {
        try
        {
            if (e.Document.Equals(doc))
            {
                doc = null;
                acciones.Encolar(Recargar);
            }
        }
        catch
        {
            // ver arriba
        }
    }

    private void Soltar()
    {
        try
        {
            uiapp.ViewActivated -= AlActivarVista;
            uiapp.Application.DocumentClosing -= AlCerrarDocumento;
        }
        catch
        {
            // Revit puede estar cerrándose.
        }
        acciones.Dispose();
    }

    private List<ZapataLeida> Elegidas(IReadOnlyList<string> ids) => ids.Select(i => zapatas.FirstOrDefault(z => z.Id == i)).OfType<ZapataLeida>().ToList();

    private static ElementId Id(string id) => long.TryParse(id, out long n) ? new ElementId(n) : ElementId.InvalidElementId;
}
