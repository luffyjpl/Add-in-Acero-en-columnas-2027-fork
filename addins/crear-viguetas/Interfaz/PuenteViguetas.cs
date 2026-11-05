using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CQEING.Viguetas.Calculo;
using CQEING.Viguetas.Revit;
using Panel = CQEING.Viguetas.Calculo.Panel;

namespace CQEING.Viguetas.Interfaz;

/// <summary>
/// Une la página de «Crear viguetas» (#951) con Revit. Leer el piso, crear y quitar van al hilo de Revit por
/// <see cref="AccionesRevit"/>; la propuesta de un panel no usa la API y se contesta enseguida, mientras se escribe.
/// </summary>
internal sealed class PuenteViguetas
{
    private readonly VentanaViguetas ventana;
    private readonly Document doc;
    private readonly AccionesRevit acciones = new();
    private PisoViguetas? piso;

    public PuenteViguetas(VentanaViguetas ventana, Document doc)
    {
        this.ventana = ventana;
        this.doc = doc;
        ventana.MensajeRecibido += Recibir;
        acciones.Fallo += texto => ventana.Enviar(ProtocoloViguetas.Aviso($"No se pudo hacer en Revit: {texto}"));
        ventana.Closed += (_, _) => acciones.Dispose();
    }

    private void Recibir(string json)
    {
        MensajeViguetas mensaje = ProtocoloViguetas.Leer(json);
        switch (mensaje)
        {
            case PedirPropuesta p:
            {
                (PropuestaViguetas? propuesta, bool hayGuias) = Propuesta(p);
                ventana.Enviar(ProtocoloViguetas.Propuesta(propuesta, p.Paneles, p.Continuas, hayGuias, Modelo(p)));
                break;
            }
            case ArmarPerimetro a:
                ventana.Enviar(ProtocoloViguetas.Perimetro(a.Ids, piso is null ? [] : piso.Perimetro(a.Ids).Select(z => (z, piso.CreadasEn(z).Count))));
                break;
            case ArmarZonas z:
                ventana.Enviar(ProtocoloViguetas.Zonas(piso is null ? [] : piso.Zonas(z.Zonas, z.Ancho).Select(x => (x, piso.CreadasEn(x).Count))));
                break;
            case CerrarViguetas:
                ventana.Close();
                break;
            case DesconocidoViguetas:
                break;
            default:
                acciones.Encolar(_ => Atender(mensaje));
                break;
        }
    }

    /// <summary>Lo que toca el modelo, en el hilo de Revit.</summary>
    private void Atender(MensajeViguetas mensaje)
    {
        if (!doc.IsValidObject)
        {
            ventana.Enviar(ProtocoloViguetas.Aviso("El modelo se cerró. Cierre esta ventana y vuelva a abrirla."));
            return;
        }
        switch (mensaje)
        {
            case ListoViguetas:
                ventana.Enviar(ProtocoloViguetas.Pisos(doc.Title, LectorPiso.Pisos(doc), LectorPiso.Tipos(doc)));
                break;
            case ElegirPiso e:
                Abrir(e.Id, e.ViguetasComoBorde);
                break;
            case CrearViguetas c:
                Crear(c);
                break;
            case QuitarCreadas q:
                Quitar(q.Paneles);
                break;
        }
    }

    private void Abrir(string id, bool viguetasComoBorde)
    {
        if (Nivel(id) is not Level nivel)
            return;
        piso = new PisoViguetas(LectorPiso.Leer(doc, nivel), viguetasComoBorde);
        ventana.Enviar(ProtocoloViguetas.Piso(piso));
        if (piso.Paneles.Count == 0)
            ventana.Enviar(ProtocoloViguetas.Aviso($"En {nivel.Name} no se encontraron paneles cerrados por vigas."));
    }

    private void Crear(CrearViguetas c)
    {
        if (piso is null || Propuesta(c.Propuesta).Propuesta is not PropuestaViguetas propuesta)
            return;
        if (Nivel(piso.Piso.Id) is not Level nivel || doc.GetElement(Id(c.Tipo)) is not FamilySymbol tipo)
        {
            ventana.Enviar(ProtocoloViguetas.Aviso("Elija un tipo de vigueta."));
            return;
        }
        List<Panel> paneles = piso.PanelesDe(c.Propuesta.Paneles);
        HashSet<string> marcadas = c.Propuesta.DelModelo.ToHashSet();
        var recortes = piso.PiezasDelModelo(paneles).Where(x => marcadas.Contains(x.Id)).GroupBy(x => x.Elemento)
            .Select(g => (Id(g.Key), PisoViguetas.Restos(piso.Elemento(g.Key)!, g.Select(x => (x.Desde, x.Hasta))))).ToList();
        (int creadas, int recortadas) = CreadorViguetas.Crear(doc, nivel, tipo, propuesta.Lineas(c.Propuesta.Continuas),
            Ids(paneles.SelectMany(piso.CreadasEn)), recortes);
        Abrir(piso.Piso.Id, piso.ViguetasComoBorde);
        string delModelo = recortadas > 0 ? $" y se quitó de {recortadas} viguetas del modelo el tramo dentro del panel" : "";
        ventana.Enviar(ProtocoloViguetas.Aviso($"Se crearon {creadas} viguetas en {Nombres(paneles)}{delModelo}. Ctrl+Z en Revit lo deshace.", bien: true));
    }

    private void Quitar(IReadOnlyList<string> ids)
    {
        if (piso is null)
            return;
        List<Panel> paneles = piso.PanelesDe(ids);
        int quitadas = CreadorViguetas.Quitar(doc, Ids(paneles.SelectMany(piso.CreadasEn)));
        Abrir(piso.Piso.Id, piso.ViguetasComoBorde);
        ventana.Enviar(ProtocoloViguetas.Aviso($"Se quitaron {quitadas} viguetas de {Nombres(paneles)}.", bien: true));
    }

    /// <summary>
    /// La propuesta pedida y si hay viguetas existentes que se puedan seguir (decisión 16): la página muestra la opción
    /// solo entonces.
    /// </summary>
    private (PropuestaViguetas? Propuesta, bool HayGuias) Propuesta(PedirPropuesta p)
    {
        if (piso is null)
            return (null, false);
        List<Panel> paneles = piso.PanelesDe(p.Paneles);
        HashSet<string> borradas = p.Borradas.ToHashSet();
        // Donde ya hay una vigueta del modelo, quitada o no, no se propone otra (decisión 28).
        List<(Punto Desde, Punto Hasta)> ocupadas = piso.PiezasDelModelo(paneles).Select(x => (x.Inicio, x.Fin)).ToList();
        PropuestaViguetas? Con(IReadOnlyList<double>? guias) =>
            PlanificadorViguetas.Planificar(paneles, p.Direccion, p.EjeAEje, p.Ancho, borradas, piso.Cruces, guias, ocupadas, piso.Vigas);
        PropuestaViguetas? siguiendo = Con(piso.Guias(p.Direccion, paneles));
        bool hay = siguiendo?.Paneles.Any(r => r.Origen == OrigenReparto.Existentes) == true;
        return (p.Seguir || !hay ? siguiendo : Con(null), hay);
    }

    private static string Nombres(List<Panel> paneles) => string.Join(", ", paneles.Select(p => p.Id));

    /// <summary>Las piezas de las viguetas del modelo en los paneles pedidos y si están marcadas (decisión 22).</summary>
    private List<(PiezaModelo Pieza, bool Quitada)> Modelo(PedirPropuesta p)
    {
        if (piso is null)
            return [];
        HashSet<string> marcadas = p.DelModelo.ToHashSet();
        return piso.PiezasDelModelo(piso.PanelesDe(p.Paneles)).Select(x => (x, marcadas.Contains(x.Id))).ToList();
    }

    private Level? Nivel(string id) => doc.GetElement(Id(id)) as Level;

    private static ElementId Id(string id) => long.TryParse(id, out long n) ? new ElementId(n) : ElementId.InvalidElementId;

    private static IEnumerable<ElementId> Ids(IEnumerable<ArmazonPiso> armazon) => armazon.Select(a => Id(a.Id)).Distinct().ToList();
}
