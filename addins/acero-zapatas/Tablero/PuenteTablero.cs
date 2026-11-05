using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
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
        }
    }

    private void Leer()
    {
        zapatas = LectorZapatas.Leer(doc);
        catalogo = TiposZapata.Catalogo(doc, reglas);
        var existentes = zapatas.ToDictionary(z => z.Id, z => doc.GetElement(Id(z.Id)) is { } e ? MarcaZapata.Contar(e) : (0, 0));
        ventana.Enviar(ProtocoloZapatas.Datos(doc.Title, zapatas, Grupos(zapatas), catalogo, existentes));
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

    private void Seleccionar(UIApplication app)
    {
        UIDocument ui = app.ActiveUIDocument;
        HashSet<string> conocidas = zapatas.Select(z => z.Id).ToHashSet();
        List<string> ids = ui.Selection.GetElementIds().Select(i => i.ToString()).Where(conocidas.Contains).ToList();
        if (ids.Count == 0)
        {
            ventana.Enviar(ProtocoloZapatas.Aviso("Elija las zapatas en Revit y pulse Terminar en la barra de opciones."));
            ids = ui.Selection.PickObjects(ObjectType.Element, new SoloZapatas(conocidas), "Elija las zapatas").Select(r => r.ElementId.ToString()).ToList();
        }
        ventana.Enviar(ProtocoloZapatas.Seleccion(ids));
        ventana.Volver();
    }

    private void Mostrar(UIApplication app, IReadOnlyList<string> ids)
    {
        List<ElementId> elementos = ids.Select(Id).Where(i => doc.GetElement(i) is not null).ToList();
        if (elementos.Count == 0)
            return;
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

    private sealed class SoloZapatas(HashSet<string> ids) : ISelectionFilter
    {
        public bool AllowElement(Element elem) => ids.Contains(elem.Id.ToString());

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
