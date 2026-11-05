using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit.Columnas;

/// <summary>
/// La sección superior de una columna (#228), armada como dice la decisión 16 del plan
/// docs/planes/2026-10-31-despiece-columnas-226-228.md y ajustada por la 17: una planta propia cortada cerca del tope,
/// debajo de los ganchos, mirando hacia abajo, con las barras cortadas y el estribo; etiquetas, cotas, recubrimiento y el
/// estribo aparte.
/// </summary>
internal static class SeccionColumna
{
    private const double M = 1 / 0.3048;
    private const int Escala = 15;

    /// <summary>El corte va así de debajo del tope: el concreto sale cortado y los ganchos quedan debajo, a la vista.</summary>
    private const double BajoTope = 0.01 * M;

    /// <summary>Una barra que termina a menos de esto del tope llega arriba: su gancho es de los de arriba.</summary>
    private const double BajoGanchos = 0.30 * M;

    /// <summary>El corte va así de debajo del final del tramo recto, donde empieza el doblez del gancho.</summary>
    private const double DebajoDelGancho = 0.03 * M;

    /// <summary>Lo que mira hacia abajo desde el corte: alcanza los estribos de arriba.</summary>
    private const double Profundidad = 0.50 * M;

    /// <summary>Una barra vertical que termina más abajo que esto del corte es una pieza de abajo: se oculta.</summary>
    private const double PiezaDeAbajo = 0.15 * M;

    // Como la sección de la viga: las etiquetas en filas encima y debajo, y debajo de ellas la cota y el recubrimiento.
    private const double FilaArriba = 0.38 * M;
    private const double FilaAbajo = 0.20 * M;
    private const double PasoEtiquetas = 0.28 * M;
    private const double CotaAncho = 0.42 * M;
    private const double TextoAncho = 0.46 * M;
    private const double CotaAlto = 0.15 * M;
    private const double TextoAlto = 0.30 * M;
    private const double Leyenda = 0.62 * M;
    private const double EstriboAparte = 0.70 * M;
    private const double AireNota = 0.01 * M;

    /// <summary>A 1:15 la caja de una nota mide 0.12 m (medido en la viga); entre una forma de estribo y la siguiente, 0.08 m.</summary>
    private const double FilaTexto = 0.13 * M;
    private const double HuecoEstribos = 0.08 * M;

    /// <summary>Los colores de traslapo (decisión 3): no están en la paleta de diámetros.</summary>
    private static readonly Color ColorA = new(0x00, 0xAC, 0xC1);
    private static readonly Color ColorB = new(0x9E, 0x9E, 0x9E);

    /// <summary>
    /// <paramref name="porTraslapo"/>: barras y etiquetas en el color de su grupo A/B; si no, en el de su diámetro. Quien
    /// llama abre la transacción.
    /// </summary>
    public static View Crear(Document doc, ColumnaLeida columna, List<Rebar> barras, TiposPropios tipos, bool porTraslapo)
    {
        Element arriba = columna.Elementos.Select(doc.GetElement).OrderByDescending(e => e.get_BoundingBox(null)?.Max.Z ?? double.MinValue).First();
        BoundingBoxXYZ caja = arriba.get_BoundingBox(null);
        double zCorte = Corte(columna, barras);
        XYZ centro = new((caja.Min.X + caja.Max.X) / 2, (caja.Min.Y + caja.Max.Y) / 2, zCorte);
        double ancho = caja.Max.X - caja.Min.X, alto = caja.Max.Y - caja.Min.Y;
        XYZ En(double x, double y) => new(x, y, zCorte);

        ViewPlan vista = Planta(doc, zCorte);
        Registro.Paso($"sección de columna: {(porTraslapo ? "traslapos" : "varillas")}, nivel {vista.GenLevel?.Name}, corte a {zCorte * 0.3048:0.00}");
        GeneradorPrevia.Recortar(vista, centro, ancho / 2 + 0.60 * M, ancho / 2 + 1.20 * M, alto / 2 + Leyenda + 0.15 * M, alto / 2 + 0.60 * M);
        Limpiar(doc, vista, columna, barras);

        // Las piezas de abajo se verían como un segundo círculo junto a cada varilla.
        List<Rebar> verticales = barras.Where(LectorColumnas.EsVertical).ToList();
        List<ElementId> abajo = verticales.Where(b => LectorColumnas.Alturas(b).Arriba < zCorte - PiezaDeAbajo)
            .Select(b => b.Id).Where(id => doc.GetElement(id).CanBeHidden(vista)).ToList();
        if (abajo.Count > 0)
            vista.HideElements(abajo);
        var arribaIds = new HashSet<ElementId>(verticales.Select(b => b.Id).Except(abajo));

        Dictionary<ElementId, GrupoTraslapo> grupos = Grupos(columna);
        Color? DeGrupo(Rebar b) => porTraslapo && grupos.TryGetValue(b.Id, out GrupoTraslapo g) ? (g == GrupoTraslapo.A ? ColorA : ColorB) : null;
        foreach (Rebar b in barras)
        {
            b.SetUnobscuredInView(vista, true);
            if (DeGrupo(b) is { } color)
                Pintar(vista, b.Id, color);
            else
                GeneradorPrevia.Pintar(vista, b.Id, b, null);
        }
        // Concreto gris, sin el rayado (hallazgo 22).
        var gris = new OverrideGraphicSettings();
        gris.SetCutBackgroundPatternId(GeneradorPrevia.Solido(doc));
        gris.SetCutBackgroundPatternColor(new Color(205, 205, 205));
        gris.SetCutForegroundPatternVisible(false);
        gris.SetSurfaceForegroundPatternVisible(false);
        foreach (ElementId id in columna.Elementos)
            vista.SetElementOverrides(id, gris);
        vista.DisplayStyle = DisplayStyle.FlatColors;
        doc.Regenerate();

        Etiquetar(doc, vista, verticales.Where(b => arribaIds.Contains(b.Id)).ToList(), centro, alto, zCorte, tipos, b => DeGrupo(b));
        Rebar? estribo = Estribo(doc, vista, columna, barras);
        Acotar(doc, vista, arriba, estribo, caja, zCorte, tipos);
        if (estribo is not null)
            SacarEstribo(doc, vista, barras, estribo, En(caja.Max.X + EstriboAparte, centro.Y), tipos);
        if (porTraslapo)
        {
            TextNote a = TextNote.Create(doc, vista.Id, En(caja.Min.X, caja.Min.Y - Leyenda), "Traslapo A", tipos.Nota(HorizontalTextAlignment.Left));
            TextNote b = TextNote.Create(doc, vista.Id, En(caja.Min.X + 0.45 * M, caja.Min.Y - Leyenda), "Traslapo B", tipos.Nota(HorizontalTextAlignment.Left));
            // La leyenda es la clave de los colores: va en color aunque se pidan los textos en negro.
            Pintar(vista, a.Id, ColorA, siempre: true);
            Pintar(vista, b.Id, ColorB, siempre: true);
        }
        doc.Regenerate();
        return vista;
    }

    /// <summary>
    /// Sin ganchos (decisión 17): 3 cm debajo de donde termina el tramo recto más bajo de las barras que llegan arriba,
    /// así los ganchos quedan sobre el corte y no se ven; y nunca más arriba que <see cref="BajoTope"/> bajo el tope.
    /// </summary>
    private static double Corte(ColumnaLeida columna, List<Rebar> barras)
    {
        double tope = columna.ZSuperior - BajoTope;
        List<double> rectos = barras.Where(LectorColumnas.EsVertical)
            .Where(b => LectorColumnas.Alturas(b).Arriba > columna.ZSuperior - BajoGanchos)
            .Select(b =>
            {
                Curve recta = GeneradorPrevia.Mayor(b);
                return Math.Max(recta.GetEndPoint(0).Z, recta.GetEndPoint(1).Z);
            }).ToList();
        return rectos.Count == 0 ? tope : Math.Min(tope, rectos.Min() - DebajoDelGancho);
    }

    /// <summary>
    /// La planta propia (decisión 9), sobre el nivel más alto que esté debajo del corte, con el corte y el rango de vista
    /// puestos a mano: de 2 cm sobre el corte hasta <see cref="Profundidad"/> debajo.
    /// </summary>
    private static ViewPlan Planta(Document doc, double zCorte)
    {
        List<Level> niveles = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(n => n.Elevation).ToList();
        Level nivel = niveles.LastOrDefault(n => n.Elevation <= zCorte) ?? niveles.First();
        List<ViewFamilyType> tipos = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().ToList();
        ViewFamilyType tipo = tipos.FirstOrDefault(t => t.ViewFamily == ViewFamily.StructuralPlan) ?? tipos.First(t => t.ViewFamily == ViewFamily.FloorPlan);
        ViewPlan vista = ViewPlan.Create(doc, tipo.Id, nivel.Id);
        vista.Name = "CQEING temporal P " + Guid.NewGuid().ToString("N");
        vista.ViewTemplateId = ElementId.InvalidElementId;
        vista.Scale = Escala;
        vista.DetailLevel = ViewDetailLevel.Fine;
        vista.DisplayStyle = DisplayStyle.Wireframe;

        double corte = zCorte - nivel.Elevation;
        PlanViewRange rango = vista.GetViewRange();
        foreach ((PlanViewPlane plano, double desplazamiento) in new[]
        {
            (PlanViewPlane.TopClipPlane, corte + 0.02 * M), (PlanViewPlane.CutPlane, corte),
            (PlanViewPlane.BottomClipPlane, corte - Profundidad), (PlanViewPlane.ViewDepthPlane, corte - Profundidad),
        })
        {
            rango.SetLevelId(plano, nivel.Id);
            rango.SetOffset(plano, desplazamiento);
        }
        vista.SetViewRange(rango);
        vista.CropBoxActive = true;
        vista.CropBoxVisible = false;
        doc.Regenerate();
        return vista;
    }

    /// <summary>Solo la pila y su acero; sin ejes, niveles ni marcas (decisión 9).</summary>
    private static void Limpiar(Document doc, View vista, ColumnaLeida columna, List<Rebar> barras)
    {
        var deja = new HashSet<ElementId>(barras.Select(b => b.Id).Concat(columna.Elementos));
        List<ElementId> ocultar = new FilteredElementCollector(doc, vista.Id).WhereElementIsNotElementType()
            .Where(e => e.Category is { CategoryType: CategoryType.Model } && !deja.Contains(e.Id) && e.CanBeHidden(vista))
            .Select(e => e.Id).ToList();
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
        foreach (BuiltInCategory c in new[] { BuiltInCategory.OST_Grids, BuiltInCategory.OST_Levels, BuiltInCategory.OST_Sections,
            BuiltInCategory.OST_SectionBox, BuiltInCategory.OST_Elev })
        {
            if (vista.CanCategoryBeHidden(new ElementId(c)))
                vista.SetCategoryHidden(new ElementId(c), true);
        }
    }

    /// <summary>
    /// El grupo de cada conjunto de Revit, por sus varillas (decisión 4). Un conjunto con barras de los dos grupos (hecho
    /// a mano) va con el de la mayoría y queda en la traza.
    /// </summary>
    private static Dictionary<ElementId, GrupoTraslapo> Grupos(ColumnaLeida columna)
    {
        var grupos = new Dictionary<ElementId, GrupoTraslapo>();
        foreach (IGrouping<ElementId, GrupoTraslapo> conjunto in columna.Varillas.Where(v => v.Grupo is not null)
            .SelectMany(v => v.Piezas.Select(p => (p.Barra, Grupo: v.Grupo!.Value))).GroupBy(x => x.Barra, x => x.Grupo))
        {
            List<IGrouping<GrupoTraslapo, GrupoTraslapo>> porGrupo = conjunto.GroupBy(g => g).OrderByDescending(g => g.Count()).ToList();
            if (porGrupo.Count > 1)
                Registro.Paso($"sección de columna: el conjunto {conjunto.Key} tiene barras de los dos grupos; va con {porGrupo[0].Key}");
            grupos[conjunto.Key] = porGrupo[0].Key;
        }
        return grupos;
    }

    private static void Pintar(View vista, ElementId id, Color color, bool siempre = false)
    {
        if (!siempre && GeneradorPrevia.textosNegros && vista.Document.GetElement(id) is TextNote)
            return;
        ElementId solido = GeneradorPrevia.Solido(vista.Document);
        var o = new OverrideGraphicSettings();
        o.SetProjectionLineColor(color);
        o.SetCutLineColor(color);
        o.SetSurfaceForegroundPatternId(solido);
        o.SetSurfaceForegroundPatternColor(color);
        o.SetCutForegroundPatternId(solido);
        o.SetCutForegroundPatternColor(color);
        vista.SetElementOverrides(id, o);
    }

    /// <summary>
    /// Una etiqueta <c>1 #4</c> con flecha por cada barra, como en la sección de la viga: las de la mitad de arriba en una
    /// fila encima de la columna y las demás en una fila debajo, ordenadas de izquierda a derecha.
    /// </summary>
    private static void Etiquetar(Document doc, View vista, List<Rebar> verticales, XYZ centro, double alto, double zCorte, TiposPropios tipos, Func<Rebar, Color?> deGrupo)
    {
        var puntos = new List<(Rebar Barra, XYZ Punto)>();
        foreach (Rebar b in verticales)
        {
            for (int i = 0; i < b.Quantity; i++)
            {
                Curve recta = b.GetTransformedCenterlineCurves(false, true, false, MultiplanarOption.IncludeOnlyPlanarCurves, i).OrderByDescending(c => c.Length).First();
                XYZ p = recta.GetEndPoint(0);
                puntos.Add((b, new XYZ(p.X, p.Y, zCorte)));
            }
        }
        void Fila(List<(Rebar Barra, XYZ Punto)> fila, double y)
        {
            List<(Rebar Barra, XYZ Punto)> orden = fila.OrderBy(f => f.Punto.X).ToList();
            for (int i = 0; i < orden.Count; i++)
            {
                double x = centro.X + (i - (orden.Count - 1) / 2.0) * PasoEtiquetas - 0.08 * M;
                (Rebar barra, XYZ punto) = orden[i];
                string tipo = doc.GetElement(barra.GetTypeId()).Name;
                int n = LectorDespiece.Numero(tipo);
                TextNote nota = TextNote.Create(doc, vista.Id, new XYZ(x, y, zCorte), "1 " + (n > 0 ? "#" + n : tipo), tipos.Nota(HorizontalTextAlignment.Left));
                nota.AddLeader(TextNoteLeaderTypes.TNLT_STRAIGHT_L).End = punto;
                if (deGrupo(barra) is { } color)
                    Pintar(vista, nota.Id, color);
                else
                    GeneradorPrevia.Pintar(vista, nota.Id, barra, null);
            }
        }
        Fila(puntos.Where(p => p.Punto.Y > centro.Y).ToList(), centro.Y + alto / 2 + FilaArriba);
        Fila(puntos.Where(p => p.Punto.Y <= centro.Y).ToList(), centro.Y - alto / 2 - FilaAbajo);
    }

    /// <summary>El estribo de más tramos que se ve en la planta (el cerrado: la grapa tiene menos).</summary>
    private static Rebar? Estribo(Document doc, View vista, ColumnaLeida columna, List<Rebar> barras)
    {
        var enLaVista = new HashSet<ElementId>(new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Rebar).ToElementIds());
        return barras.Where(b => !LectorColumnas.EsVertical(b) && enLaVista.Contains(b.Id))
            .OrderByDescending(b => GeneradorPrevia.Curvas(b, false).Count).ThenByDescending(b => LectorColumnas.Alturas(b).Arriba).FirstOrDefault();
    }

    /// <summary>
    /// Ancho y alto con cotas de Revit contra las caras del elemento de arriba, y el recubrimiento en texto por la cara
    /// exterior del estribo, como en la viga. Sin caras planas (columna circular) no hay cotas.
    /// </summary>
    private static void Acotar(Document doc, View vista, Element arriba, Rebar? estribo, BoundingBoxXYZ caja, double zCorte, TiposPropios tipos)
    {
        try
        {
            List<PlanarFace> Caras(XYZ eje) => GeneradorPrevia.Caras(arriba, vista).Where(c => Math.Abs(c.FaceNormal.DotProduct(eje)) > 0.99)
                .OrderBy(c => c.Origin.DotProduct(eje)).ToList();
            List<PlanarFace> enX = Caras(XYZ.BasisX), enY = Caras(XYZ.BasisY);
            if (enX.Count < 2 || enY.Count < 2)
            {
                Registro.Paso("sección de columna: sin caras planas para las cotas (¿columna circular?)");
                return;
            }
            double x0 = enX[0].Origin.X, x1 = enX[^1].Origin.X, y0 = enY[0].Origin.Y, y1 = enY[^1].Origin.Y;
            void Cota(Reference a, Reference b, XYZ desde, XYZ hacia)
            {
                var referencias = new ReferenceArray();
                referencias.Append(a);
                referencias.Append(b);
                Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(desde, hacia), referencias);
                if (tipos.Cota is { } tipo)
                    cota.DimensionType = tipo;
                doc.Regenerate();
            }
            Cota(enX[0].Reference, enX[^1].Reference, new XYZ(x0, y0 - CotaAncho, zCorte), new XYZ(x1, y0 - CotaAncho, zCorte));
            // Las referencias dejan de valer al crear una cota (como en la viga): se leen de nuevo.
            enY = Caras(XYZ.BasisY);
            Cota(enY[0].Reference, enY[^1].Reference, new XYZ(x0 - CotaAlto, y0, zCorte), new XYZ(x0 - CotaAlto, y1, zCorte));

            if (estribo is null)
                return;
            double d = ((RebarBarType)doc.GetElement(estribo.GetTypeId())).BarNominalDiameter;
            IList<Curve> tramos = GeneradorPrevia.Curvas(estribo, true);
            List<Curve> esquinas = tramos.Where(c => c is Arc).ToList();
            List<XYZ> puntos = (esquinas.Count > 0 ? esquinas : tramos).SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) }).ToList();
            double ex0 = puntos.Min(p => p.X) - d / 2, ex1 = puntos.Max(p => p.X) + d / 2, ey0 = puntos.Min(p => p.Y) - d / 2, ey1 = puntos.Max(p => p.Y) + d / 2;
            TextNote.Create(doc, vista.Id, new XYZ((x0 + x1) / 2, y0 - TextoAncho, zCorte),
                GeneradorPrevia.Cadena(ex0 - x0, ex1 - ex0, x1 - ex1), tipos.Nota(HorizontalTextAlignment.Center));
            TextNoteOptions vertical = tipos.Nota(HorizontalTextAlignment.Center);
            vertical.Rotation = Math.PI / 2;
            TextNote.Create(doc, vista.Id, new XYZ(x0 - TextoAlto, (y0 + y1) / 2, zCorte), GeneradorPrevia.Cadena(ey0 - y0, ey1 - ey0, y1 - ey1), vertical);
        }
        catch (Exception e)
        {
            // Mejor sin cotas que sin sección.
            Registro.Paso("sección de columna: cotas no creadas: " + e.Message);
        }
    }

    /// <summary>
    /// Cada forma de estribo aparte, a la derecha, con sus medidas (rotación 0: queda igual que en la sección, hallazgo
    /// 21), y debajo su cantidad en toda la columna, su largo y su gancho: <c>46 #3 L= 0.99 G= 0.08</c>. Primero el
    /// principal y después los menores (grapas, cajas), apilados hacia abajo, como <c>ResumirEstribos</c> en la viga
    /// (decisión 18). Revit solo dibuja el detalle de una barra que se ve: de una forma que no se ve va solo el texto.
    /// </summary>
    private static void SacarEstribo(Document doc, View vista, List<Rebar> barras, Rebar principal, XYZ punto, TiposPropios tipos)
    {
        string Clave(Rebar b) => $"{b.GetTypeId()}|{b.GetShapeId()}|{Math.Round(LectorDespiece.LargoBarra(b) * 304.8)}";
        List<IGrouping<string, Rebar>> formas = barras.Where(b => !LectorColumnas.EsVertical(b)).GroupBy(Clave)
            .OrderByDescending(g => g.Key == Clave(principal) ? int.MaxValue : GeneradorPrevia.Curvas(g.First(), false).Count).ToList();
        var enLaVista = new HashSet<ElementId>(new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Rebar).ToElementIds());
        double? tope = null;
        foreach (IGrouping<string, Rebar> forma in formas)
        {
            Rebar b = forma.Key == Clave(principal) ? principal : forma.FirstOrDefault(x => enLaVista.Contains(x.Id)) ?? forma.First();
            double piso = tope ?? punto.Y;
            try
            {
                if (enLaVista.Contains(b.Id))
                {
                    Element d = RebarBendingDetail.Create(doc, vista.Id, b.Id, 0, tipos.Barra, punto, 0);
                    GeneradorPrevia.Pintar(vista, d.Id, b, 3);
                    doc.Regenerate();
                    BoundingBoxXYZ m = d.get_BoundingBox(vista);
                    // Debajo del anterior: su borde de arriba donde terminó el texto de la forma anterior.
                    double dy = tope is { } t ? t - m.Max.Y : 0;
                    if (dy != 0)
                        RebarBendingDetail.SetPosition(d, punto + XYZ.BasisY * dy);
                    piso = m.Min.Y + dy;
                    // El largo de los ganchos del principal en texto, donde hay espacio, como en la viga (hallazgo 36).
                    if (b == principal)
                        GeneradorPrevia.EtiquetarGancho(doc, vista, b, punto, punto.DotProduct(vista.UpDirection), tipos);
                }
                else
                    Registro.Paso($"sección de columna: estribo {b.Id} no se ve en la planta, va solo su texto");
            }
            catch (Exception e)
            {
                Registro.Paso($"sección de columna: estribo {b.Id} sin detalle: {e.Message}");
            }
            TextNote nota = TextNote.Create(doc, vista.Id, new XYZ(punto.X, piso - AireNota, punto.Z),
                TextosDespiece.Estribo(doc.GetElement(b.GetTypeId()).Name, forma.Sum(x => x.Quantity), LectorDespiece.LargoBarra(b) * 0.3048, GeneradorPrevia.Gancho(b) * 0.3048),
                tipos.Nota(HorizontalTextAlignment.Center));
            GeneradorPrevia.Pintar(vista, nota.Id, b, null);
            tope = piso - AireNota - FilaTexto - HuecoEstribos;
        }
    }
}
