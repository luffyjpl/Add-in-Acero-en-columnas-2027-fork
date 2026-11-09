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
            case VaciarPanel v:
                Vaciar(v.Id);
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
        if (piso is null)
            return;
        // Decisión 34: con solo vacíos marcados no hay propuesta; se llevan a los otros pisos igual.
        PropuestaViguetas? propuesta = c.Propuesta.Paneles.Count > 0 ? Propuesta(c.Propuesta).Propuesta : null;
        if (propuesta is null && c.LosVaciados.Count == 0)
            return;
        if (Nivel(piso.Piso.Id) is not Level nivel || doc.GetElement(Id(c.Tipo)) is not FamilySymbol tipo)
        {
            ventana.Enviar(ProtocoloViguetas.Aviso("Elija un tipo de vigueta."));
            return;
        }
        List<Panel> paneles = piso.PanelesDe(c.Propuesta.Paneles);
        HashSet<string> marcadas = c.Propuesta.DelModelo.ToHashSet();
        List<PiezaModelo> piezas = piso.PiezasDelModelo(paneles).Where(x => marcadas.Contains(x.Id)).ToList();
        var recortes = piezas.GroupBy(x => x.Elemento)
            .Select(g => (Id(g.Key), PisoViguetas.Restos(piso.Elemento(g.Key)!, g.Select(x => (x.Desde, x.Hasta))))).ToList();
        List<(Punto Inicio, Punto Fin)> delModelo = piezas.Select(x => (x.Inicio, x.Fin)).ToList();
        List<(Punto Desde, Punto Hasta)> lineas = propuesta?.Lineas(c.Propuesta.Continuas) ?? [];
        var trabajos = new List<TrabajoViguetas>();
        var omitidas = new List<string>();
        // Decisión 34: en el piso abierto, lo que se ve; en los demás se repite todo: viguetas, vacíos y recortes.
        foreach (string idPiso in (c.Pisos is { Count: > 0 } ? c.Pisos : [piso.Piso.Id]).Distinct())
        {
            if (Nivel(idPiso) is not Level destino)
                continue;
            if (idPiso == piso.Piso.Id)
            {
                if (lineas.Count > 0)
                    trabajos.Add(new TrabajoViguetas(destino, lineas, ConIds(piso.CreadasARecortar(paneles)), recortes));
                continue;
            }
            var otro = new PisoViguetas(LectorPiso.Leer(doc, destino), piso.ViguetasComoBorde);
            var quitar = otro.RecortesDeCopia(paneles, c.LosVaciados, delModelo);
            CopiaEnPiso copia = otro.Copiar(lineas, quitar, c.Propuesta.EjeAEje);
            // Si ahí no se puede crear ninguna, de ese piso solo se cortan los vacíos.
            if (copia.Lineas.Count == 0)
                quitar = otro.RecortesDeCopia([], c.LosVaciados, []);
            if (copia.Lineas.Count > 0 || quitar.Count > 0)
                trabajos.Add(new TrabajoViguetas(destino, copia.Lineas, [], ConIds(quitar)));
            if (copia.SinApoyo + copia.Ocupadas > 0)
                omitidas.Add($"{destino.Name}: {copia.SinApoyo + copia.Ocupadas} sin apoyo o donde ya hay viguetas");
        }
        string detalle = omitidas.Count > 0 ? $" Omitidas en {string.Join("; ", omitidas)}." : "";
        if (trabajos.Count == 0)
        {
            ventana.Enviar(ProtocoloViguetas.Aviso($"No hubo nada que crear ni cortar en esos pisos.{detalle}"));
            return;
        }
        List<(int Creadas, int Recortadas)> resultados = CreadorViguetas.Crear(doc, tipo, trabajos);
        Abrir(piso.Piso.Id, piso.ViguetasComoBorde);
        int total = resultados.Sum(r => r.Creadas);
        int recortadas = trabajos.Zip(resultados).Where(x => x.First.Nivel.Id == nivel.Id).Sum(x => x.Second.Recortadas);
        string delModeloTexto = recortadas > 0 ? $" y se quitó de {recortadas} viguetas del modelo el tramo dentro del panel" : "";
        bool soloEste = trabajos.Count == 1 && trabajos[0].Nivel.Id == nivel.Id;
        string donde = soloEste ? Nombres(paneles) : string.Join(", ", trabajos.Zip(resultados, (t, r) => $"{t.Nivel.Name} ({r.Creadas})"));
        string hecho = total > 0
            ? $"Se crearon {total} viguetas en {donde}{delModeloTexto}."
            : $"Se llevaron los vacíos a {string.Join(", ", trabajos.Select(t => t.Nivel.Name))}.";
        ventana.Enviar(ProtocoloViguetas.Aviso($"{hecho}{detalle} Ctrl+Z en Revit lo deshace.", bien: omitidas.Count == 0));
    }

    /// <summary>Decisión 30: el panel queda vacío: se cortan en Revit las viguetas que lo cruzan.</summary>
    private void Vaciar(string id)
    {
        if (piso is null || piso.Panel(id) is not Panel panel)
            return;
        var recortes = ConIds(piso.TramosEnPanel(panel));
        int cortadas = recortes.Count == 0 ? 0 : CreadorViguetas.Vaciar(doc, recortes);
        Abrir(piso.Piso.Id, piso.ViguetasComoBorde);
        ventana.Enviar(ProtocoloViguetas.Aviso(cortadas == 0
            ? $"{id} queda como vacío: no tenía viguetas."
            : $"{id} queda como vacío: se cortaron {cortadas} viguetas. Ctrl+Z en Revit lo deshace.", bien: true));
    }

    private void Quitar(IReadOnlyList<string> ids)
    {
        if (piso is null)
            return;
        List<Panel> paneles = piso.PanelesDe(ids);
        int quitadas = CreadorViguetas.Quitar(doc, ConIds(piso.CreadasARecortar(paneles)));
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
        List<Panel> paneles = piso.ConVacios(piso.PanelesDe(p.Paneles), p.LosVacios, p.Ancho);
        HashSet<string> borradas = p.Borradas.ToHashSet();
        bool EnVacio(Panel q) => p.LosVacios.Any(v => Geometria.Dentro(q.Eje, Geometria.Centro(v)));
        // Donde ya hay una vigueta del modelo, quitada o no, no se propone otra (decisión 28).
        List<(Punto Desde, Punto Hasta)> ocupadas = piso.PiezasDelModelo(paneles).Select(x => (x.Inicio, x.Fin)).ToList();
        // #954, decisión 28: en Paneles, cada vigueta sigue por su línea por los paneles no elegidos.
        List<Panel>? resto = p.Prolongar
            ? piso.Paneles.Where(q => !p.Paneles.Contains(q.Id) && !EnVacio(q)).ToList()
            : null;
        // Decisión 31: también hasta los voladizos.
        List<Panel>? voladizos = p.Prolongar ? piso.Voladizos.Where(q => !EnVacio(q)).ToList() : null;
        // Decisión 33: un vacío solo quita su panel; la línea sigue del otro lado.
        List<Panel>? saltar = p.Prolongar ? piso.Paneles.Concat(piso.Voladizos).Where(q => !p.Paneles.Contains(q.Id) && EnVacio(q)).ToList() : null;
        List<(double Cota, double Desde, double Hasta)>? lineas = p.Prolongar ? piso.LineasExistentes(p.Direccion, paneles) : null;
        PropuestaViguetas? Con(IReadOnlyList<double>? guias) =>
            PlanificadorViguetas.Planificar(paneles, p.Direccion, p.EjeAEje, p.Ancho, borradas, piso.Cruces, guias, ocupadas, piso.Vigas, resto, lineas, voladizos, saltar);
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

    private static List<(ElementId Id, List<(double Desde, double Hasta)> Restos)> ConIds(IEnumerable<(ArmazonPiso Vigueta, List<(double Desde, double Hasta)> Restos)> recortes) =>
        recortes.Select(x => (Id(x.Vigueta.Id), x.Restos)).ToList();
}
