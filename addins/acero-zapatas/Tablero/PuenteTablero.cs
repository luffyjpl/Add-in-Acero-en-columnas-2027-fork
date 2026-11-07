using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Zapatas.Calculo;
using CQEING.Zapatas.Revit;

namespace CQEING.Zapatas.Tablero;

/// <summary>
/// Une la página del tablero con Revit (#252). Calcular no usa la API y se contesta enseguida; leer, seleccionar y generar
/// van al hilo de Revit por <see cref="AccionesRevit"/>.
/// </summary>
internal sealed class PuenteTablero
{
    private readonly VentanaTablero ventana;
    private readonly Document doc;
    private readonly AccionesRevit acciones = new();
    private readonly Reglas reglas = Reglas.Generales;
    private List<ZapataLeida> zapatas = [];
    private CatalogoZapatas catalogo = new([], [], []);
    private BarraSeleccion? barra;

    public PuenteTablero(VentanaTablero ventana, Document doc)
    {
        this.ventana = ventana;
        this.doc = doc;
        ventana.MensajeRecibido += Recibir;
        acciones.Fallo += texto => ventana.Enviar(ProtocoloZapatas.Aviso($"No se pudo hacer en Revit: {texto}"));
        ventana.Closed += (_, _) => acciones.Dispose();
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
        if (!doc.IsValidObject)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso("El modelo se cerró. Cierre esta ventana y vuelva a abrirla."));
            return;
        }
        switch (mensaje)
        {
            case ListoZapatas:
                Leer();
                break;
            case SeleccionarEnRevit:
                Seleccionar(app);
                break;
            case MostrarEnRevit m:
                Mostrar(app, m.Ids);
                break;
            case AplicarZapatas a:
                Aplicar(app, a);
                break;
            case MarcarTerminadas m:
                Marcar(m);
                break;
        }
    }

    /// <summary>La palomita del tablero: pone o quita la marca en las zapatas, en una sola operación de Revit.</summary>
    private void Marcar(MarcarTerminadas m)
    {
        using (var t = new Transaction(doc, m.Terminada ? "Marcar zapatas terminadas" : "Desmarcar zapatas terminadas"))
        {
            t.Start();
            foreach (string id in m.Ids)
                if (doc.GetElement(Id(id)) is { } zapata)
                    MarcaTerminada.Marcar(zapata, m.Terminada);
            t.Commit();
        }
        Leer();
    }

    private void Leer()
    {
        zapatas = LectorZapatas.Leer(doc);
        catalogo = TiposZapata.Catalogo(doc, reglas);
        var existentes = zapatas.ToDictionary(z => z.Id, z => doc.GetElement(Id(z.Id)) is { } e ? MarcaZapata.Contar(e) : (0, 0));
        HashSet<string> terminadas = zapatas.Where(z => MarcaTerminada.Tiene(doc.GetElement(Id(z.Id)))).Select(z => z.Id).ToHashSet();
        ventana.Enviar(ProtocoloZapatas.Datos(doc.Title, zapatas, Grupos(zapatas), catalogo, existentes, terminadas));
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
        ResultadoZapata r = MotorParrillas.Calcular(elegidas[0].Entrada, c.Config, reglas);
        avisos.AddRange(r.Avisos);
        errores.AddRange(r.Errores);
        foreach ((string nombre, ConfigDireccion d) in new[] { ("X", c.Config.X), ("Y", c.Config.Y) })
            if (Grados(d.Terminacion) is int grados && ReglasZapata.AvisoTabla(reglas, d.Numero, grados, catalogo.Barras.FirstOrDefault(b => b.Numero == d.Numero)?.Doblado) is { } aviso)
                avisos.Add($"Dirección {nombre}: {aviso}");
        ventana.Enviar(ProtocoloZapatas.Resultado(r, c.Ids, avisos, errores));
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
    private void Seleccionar(UIApplication app)
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
    private void Mostrar(UIApplication app, IReadOnlyList<string> ids)
    {
        List<ElementId> elementos = ids.Select(Id).Where(i => doc.GetElement(i) is not null).ToList();
        if (elementos.Count == 0)
            return;
        ventana.Apartar();
        app.ActiveUIDocument.Selection.SetElementIds(elementos);
        app.ActiveUIDocument.ShowElements(elementos);
    }

    private void Aplicar(UIApplication app, AplicarZapatas a)
    {
        List<ZapataLeida> elegidas = Elegidas(a.Ids);
        if (elegidas.Count == 0)
            return;
        if (Grupos(elegidas).Count > 1)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso("Las zapatas elegidas no son iguales: no se les puede aplicar la misma configuración."));
            return;
        }
        ResumenGeneracion r = GeneradorZapatas.Generar(doc, elegidas, a.Config, new EleccionTipos(a.FormaX, a.FormaY, a.GanchoX, a.GanchoY), reglas, app.ActiveUIDocument.ActiveView);
        Leer();
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

    private List<ZapataLeida> Elegidas(IReadOnlyList<string> ids) => ids.Select(i => zapatas.FirstOrDefault(z => z.Id == i)).OfType<ZapataLeida>().ToList();

    private static ElementId Id(string id) => long.TryParse(id, out long n) ? new ElementId(n) : ElementId.InvalidElementId;
}
