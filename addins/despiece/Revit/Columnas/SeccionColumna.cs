using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Despiece.Calculo;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Despiece.Revit.Columnas;

/// <summary>
/// La sección de una columna (#228), armada como dice la decisión 16 del plan
/// docs/planes/2026-10-31-despiece-columnas-226-228.md: una planta propia mirando hacia abajo, con las barras cortadas y el
/// estribo; cotas, recubrimiento, leyenda y el estribo aparte. Se corta al comienzo de la columna, donde las barras
/// están en su lugar (#280, decisión 30 del plan docs/planes/2026-11-05-reunion-despiece-287.md).
/// </summary>
internal static class SeccionColumna
{
    private const double M = 1 / 0.3048;
    /// <summary>La que se eligió en el tablero (#272).</summary>
    private static int Escala => Letra.EscalaSeccion;

    /// <summary>El corte va así de sobre la base del primer elemento con la sección de la columna (decisión 30).</summary>
    private const double SobreLaBase = 0.15 * M;

    /// <summary>Y nunca a menos de esto bajo el primer traslapo, donde las barras se corren.</summary>
    private const double BajoElTraslapo = 0.03 * M;

    /// <summary>Lo que se deja entre la base del elemento y el fondo del rango de vista: el dado y la zapata no se ven.</summary>
    private const double SobreElFondo = 0.005 * M;

    /// <summary>Dos medidas de sección iguales al centímetro.</summary>
    private const double AlCentimetro = 0.01 * M;

    /// <summary>Una barra vertical que termina más abajo que esto del corte es una pieza de abajo: se oculta.</summary>
    private const double PiezaDeAbajo = 0.15 * M;

    // Sin etiquetas por barra (#281): la leyenda de colores dice qué es cada una. Debajo de la columna quedan el
    // recubrimiento, la cota total y la leyenda; arriba, nada.
    /// <summary>Del cuadrito de color de la leyenda a su texto (decisión 22 del plan de #287).</summary>
    private static double AnchoCuadro => 0.09 * M * Letra.Factor;
    // Primero (más cerca de la columna) las medidas parciales, el recubrimiento `0.04 · 0.22 · 0.04`, y después la cota
    // total (#282, Santiago en la reunión del 05-nov: "las primeras que van siempre son las pequeñas y después va la
    // total"). Abajo, la caja del texto baja 0.09 m desde su punto; la cota va debajo, con su número encima de la línea.
    private static double TextoAncho => 0.12 * M * Letra.Factor;
    private static double CotaAncho => 0.32 * M * Letra.Factor;
    private static double TextoAlto => 0.15 * M * Letra.Factor;
    private static double CotaAlto => 0.32 * M * Letra.Factor;
    private static double Leyenda => 0.50 * M * Letra.Factor;
    private static double EstriboAparte => 0.70 * M * Letra.Factor;
    private static double AireNota => 0.01 * M * Letra.Papel;

    /// <summary>A 1:15 la caja de una nota mide 0.12 m (medido en la viga); entre una forma de estribo y la siguiente, 0.08 m.</summary>
    private static double FilaTexto => 0.13 * M * Letra.Factor;
    private static double HuecoEstribos => 0.08 * M * Letra.Papel;

    /// <summary>Los colores de traslapo (decisión 3): no están en la paleta de diámetros.</summary>
    private static readonly Color ColorA = new(0x00, 0xAC, 0xC1);
    private static readonly Color ColorB = new(0x9E, 0x9E, 0x9E);

    /// <summary>
    /// <paramref name="porTraslapo"/>: barras y etiquetas en el color de su grupo A/B; si no, en el de su diámetro. Quien
    /// llama abre la transacción.
    /// </summary>
    public static View Crear(Document doc, ColumnaLeida columna, List<Rebar> barras, TiposPropios tipos, bool porTraslapo)
    {
        Letra.EnSeccion();
        Element arriba = Comienzo(doc, columna);
        BoundingBoxXYZ caja = arriba.get_BoundingBox(null);
        double zCorte = Corte(columna, caja.Min.Z);
        XYZ centro = new((caja.Min.X + caja.Max.X) / 2, (caja.Min.Y + caja.Max.Y) / 2, zCorte);
        double ancho = caja.Max.X - caja.Min.X, alto = caja.Max.Y - caja.Min.Y;
        XYZ En(double x, double y) => new(x, y, zCorte);

        ViewPlan vista = Planta(doc, zCorte, caja.Min.Z + SobreElFondo);
        Registro.Paso($"sección de columna: {(porTraslapo ? "traslapos" : "varillas")}, nivel {vista.GenLevel?.Name}, corte a {zCorte * 0.3048:0.00} en el elemento {arriba.Id} (base {caja.Min.Z * 0.3048:0.00})");
        GeneradorPrevia.Recortar(vista, centro, ancho / 2 + 0.60 * M * Letra.Factor, ancho / 2 + 1.20 * M * Letra.Factor, alto / 2 + Leyenda + 0.15 * M * Letra.Factor, alto / 2 + 0.10 * M * Letra.Factor);
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

        // Las piezas corridas por el traslapo se dibujan en su lugar (decisión 34 del plan de #287): se ocultan y va un
        // círculo relleno donde está la pieza de su varilla que no se corrió.
        bool rectangular = GeneradorPrevia.Caras(arriba, vista).Any(c => Math.Abs(c.FaceNormal.X) > 0.99);
        // Solo las piezas que cruzan la altura del corte: las de arriba empiezan más alto y no se ven (con ellas se contaban 13).
        var cruzan = new HashSet<ElementId>(verticales.Where(b => LectorColumnas.Alturas(b) is var (z0, z1) && z0 <= zCorte && z1 >= zCorte).Select(b => b.Id));
        Dictionary<ElementId, XYZ> corridas = rectangular ? Corridas(doc, columna, cruzan, centro) : [];
        List<ElementId> ocultarCorridas = corridas.Keys.Where(id => doc.GetElement(id).CanBeHidden(vista)).ToList();
        if (ocultarCorridas.Count > 0)
            vista.HideElements(ocultarCorridas);
        foreach (Rebar b in barras)
        {
            b.SetUnobscuredInView(vista, true);
            if (DeGrupo(b) is { } color)
                Pintar(vista, b.Id, color);
            else
                GeneradorPrevia.Pintar(vista, b.Id, b, null);
        }
        foreach ((ElementId id, XYZ lugar) in corridas)
        {
            var barra = (Rebar)doc.GetElement(id);
            if (Circulo(doc, vista, lugar, ((RebarBarType)doc.GetElement(barra.GetTypeId())).BarNominalDiameter) is { } circulo)
            {
                if (DeGrupo(barra) is { } color)
                    Pintar(vista, circulo, color);
                else
                    GeneradorPrevia.Pintar(vista, circulo, barra, null);
            }
        }
        Registro.Paso($"sección de columna: {corridas.Count} piezas corridas dibujadas en su lugar");
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

        Rebar? estribo = Estribo(doc, vista, columna, barras);
        Acotar(doc, vista, arriba, estribo, caja, zCorte, tipos);
        if (estribo is not null)
            SacarEstribo(doc, vista, barras, estribo, En(caja.Max.X + EstriboAparte, centro.Y), tipos);
        List<Rebar> cortadas = verticales.Where(b => arribaIds.Contains(b.Id)).ToList();
        List<(string Texto, Color Color)> leyenda = porTraslapo
            ? [("Traslapo A", ColorA), ("Traslapo B", ColorB)]
            : cortadas.GroupBy(b => doc.GetElement(b.GetTypeId()).Name)
                .OrderByDescending(g => LectorDespiece.Numero(g.Key))
                .Select(g =>
                {
                    int n = LectorDespiece.Numero(g.Key);
                    (byte r, byte v, byte a) = ColoresDiametro.Rgb(n);
                    return ($"{g.Sum(b => b.Quantity)} {(n > 0 ? "#" + n : g.Key)}", new Color(r, v, a));
                }).ToList();
        // Una debajo de otra: el cuadrito en el color y el texto en negro (decisión 22 del plan de #287).
        for (int i = 0; i < leyenda.Count; i++)
        {
            double y = caja.Min.Y - Leyenda - i * FilaTexto;
            TextNote cuadro = TextNote.Create(doc, vista.Id, En(caja.Min.X, y), "■", tipos.Nota(HorizontalTextAlignment.Left));
            // El cuadrito es la clave de los colores: va en color aunque se pidan los textos en negro.
            Pintar(vista, cuadro.Id, leyenda[i].Color, siempre: true);
            TextNote.Create(doc, vista.Id, En(caja.Min.X + AnchoCuadro, y), leyenda[i].Texto, tipos.Nota(HorizontalTextAlignment.Left));
        }
        // El recorte de abajo crece con las filas de la leyenda.
        GeneradorPrevia.Recortar(vista, centro, ancho / 2 + 0.60 * M * Letra.Factor, ancho / 2 + 1.20 * M * Letra.Factor,
            alto / 2 + Leyenda + Math.Max(0, leyenda.Count - 1) * FilaTexto + 0.15 * M * Letra.Factor, alto / 2 + 0.10 * M * Letra.Factor);
        Registro.Paso("sección de columna: leyenda " + string.Join(", ", leyenda.Select(l => l.Texto)));
        doc.Regenerate();
        return vista;
    }

    /// <summary>
    /// Las barras cortadas que no están en su lugar, con el lugar donde van (decisiones 34 y 35 del plan de #287). El lugar
    /// sale de <see cref="PosicionesIdeales"/>: las esquinas tocan las dos caras y las de cada cara se reparten entre sus
    /// esquinas. Solo en columnas rectangulares.
    /// </summary>
    private static Dictionary<ElementId, XYZ> Corridas(Document doc, ColumnaLeida columna, HashSet<ElementId> cortadas, XYZ centro)
    {
        const double Igual = 0.005 * M;
        var corridas = new Dictionary<ElementId, XYZ>();
        var varillas = new List<List<(ElementId Barra, XYZ Punto)>>();
        foreach (Varilla v in columna.Varillas)
        {
            List<(ElementId, XYZ)> piezas = v.Piezas
                .Select(p => (p.Barra, Punto: doc.GetElement(p.Barra) is Rebar r ? GeneradorPrevia.Mayor(r).GetEndPoint(0) : null))
                .Where(p => p.Punto is not null).Select(p => (p.Barra, p.Punto!)).ToList();
            if (piezas.Count > 0)
                varillas.Add(piezas);
        }
        IReadOnlyList<PuntoModelo>? ideales = PosicionesIdeales.Calcular(
            varillas.Select(v => (IReadOnlyList<PuntoModelo>)v.Select(p => new PuntoModelo(p.Punto.X, p.Punto.Y)).ToList()).ToList(),
            new PuntoModelo(centro.X, centro.Y));
        if (ideales is null)
        {
            Registro.Paso("sección de columna: no se reconocieron las cuatro esquinas; las barras van como están");
            return corridas;
        }
        for (int i = 0; i < varillas.Count; i++)
        {
            // Para medir en Revit si el lugar sale bien (decisión 35): cada pieza, si está cortada, y el lugar calculado.
            Registro.Paso($"sección de columna: varilla {i}: " + string.Join(", ", varillas[i].Select(p =>
                $"{p.Barra}{(cortadas.Contains(p.Barra) ? "*" : "")} ({p.Punto.X * 0.3048:0.000}, {p.Punto.Y * 0.3048:0.000})"))
                + $" -> lugar ({ideales[i].X * 0.3048:0.000}, {ideales[i].Y * 0.3048:0.000})");
            foreach ((ElementId barra, XYZ punto) in varillas[i].Where(p => cortadas.Contains(p.Barra)))
            {
                if (Math.Abs(punto.X - ideales[i].X) > Igual || Math.Abs(punto.Y - ideales[i].Y) > Igual)
                    corridas[barra] = new XYZ(ideales[i].X, ideales[i].Y, punto.Z);
            }
        }
        return corridas;
    }

    /// <summary>Un círculo relleno del diámetro de la barra en <paramref name="lugar"/>, en la planta. Null si Revit no lo acepta.</summary>
    private static ElementId? Circulo(Document doc, ViewPlan vista, XYZ lugar, double diametro)
    {
        try
        {
            double z = vista.GenLevel?.Elevation ?? lugar.Z;
            var c = new XYZ(lugar.X, lugar.Y, z);
            double r = diametro / 2;
            var lazo = new CurveLoop();
            lazo.Append(Arc.Create(c, r, 0, Math.PI, XYZ.BasisX, XYZ.BasisY));
            lazo.Append(Arc.Create(c, r, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
            return FilledRegion.Create(doc, TipoRelleno(doc), vista.Id, [lazo]).Id;
        }
        catch (Exception e)
        {
            Registro.Paso("sección de columna: círculo no creado: " + e.Message);
            return null;
        }
    }

    /// <summary>Un relleno sólido para los círculos: el del proyecto que ya lo sea o uno propio.</summary>
    private static ElementId TipoRelleno(Document doc)
    {
        ElementId solido = GeneradorPrevia.Solido(doc);
        List<FilledRegionType> tipos = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().ToList();
        if (tipos.FirstOrDefault(t => t.Name == "CQ Despiece barra") is { } propio)
            return propio.Id;
        var nuevo = (FilledRegionType)tipos.First().Duplicate("CQ Despiece barra");
        nuevo.ForegroundPatternId = solido;
        nuevo.ForegroundPatternColor = new Color(0, 0, 0);
        return nuevo.Id;
    }

    /// <summary>
    /// El comienzo de la columna (decisión 30): el elemento más bajo de la pila con la sección de la columna, que es la
    /// común a toda la pila. Un dado más ancho abajo no cuenta. Si ninguno la tiene, el más bajo.
    /// </summary>
    private static Element Comienzo(Document doc, ColumnaLeida columna)
    {
        double ancho = columna.Elemento.AnchoMm / 304.8, alto = columna.Elemento.AltoMm / 304.8;
        List<Element> pila = columna.Elementos.Select(doc.GetElement).Where(e => e.get_BoundingBox(null) is not null)
            .OrderBy(e => e.get_BoundingBox(null).Min.Z).ToList();
        return pila.FirstOrDefault(e =>
        {
            BoundingBoxXYZ c = e.get_BoundingBox(null);
            return Math.Abs(c.Max.X - c.Min.X - ancho) < AlCentimetro && Math.Abs(c.Max.Y - c.Min.Y - alto) < AlCentimetro;
        }) ?? pila.First();
    }

    /// <summary>
    /// <see cref="SobreLaBase"/> sobre la base del elemento, sin pasar de <see cref="BajoElTraslapo"/> bajo el primer
    /// traslapo (donde empieza la segunda pieza de cualquier varilla), y nunca por debajo de 1 cm sobre la base.
    /// </summary>
    private static double Corte(ColumnaLeida columna, double zBase)
    {
        List<double> traslapos = columna.Varillas.Where(v => v.Piezas.Count > 1).Select(v => v.Piezas[1].ZInferior).ToList();
        double corte = zBase + SobreLaBase;
        if (traslapos.Count > 0)
            corte = Math.Min(corte, traslapos.Min() - BajoElTraslapo);
        return Math.Max(corte, zBase + AlCentimetro);
    }

    /// <summary>
    /// La planta propia (decisión 9 del plan de columnas), sobre el nivel más alto que esté debajo del corte, con el corte
    /// y el rango de vista puestos a mano: de 2 cm sobre el corte hasta <paramref name="zFondo"/>.
    /// </summary>
    private static ViewPlan Planta(Document doc, double zCorte, double zFondo)
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
            (PlanViewPlane.BottomClipPlane, zFondo - nivel.Elevation), (PlanViewPlane.ViewDepthPlane, zFondo - nivel.Elevation),
        })
        {
            rango.SetLevelId(plano, nivel.Id);
            rango.SetOffset(plano, desplazamiento);
        }
        vista.SetViewRange(rango);
        // Sin esto, lo que cuelga por debajo del fondo se dibuja entero: los ganchos de la zapata (decisión 33 del plan de
        // #287). "Depth Clipping": 0 no recorta, 1 recorta con línea, 2 recorta sin línea; se anota para comprobarlo.
        if (vista.get_Parameter(BuiltInParameter.VIEW_BACK_CLIPPING) is { IsReadOnly: false } recorte)
        {
            recorte.Set(2);
            Registro.Paso($"sección de columna: recorte en profundidad = {recorte.AsInteger()} ({recorte.AsValueString()})");
        }
        else
            Registro.Paso("sección de columna: la planta no deja recortar en profundidad");
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
        Importados.Ocultar(doc, vista);
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
    internal static Dictionary<ElementId, GrupoTraslapo> Grupos(ColumnaLeida columna)
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
        // Por tipo, largo y gancho al centímetro, no por la forma de Revit: grapas de formas distintas con la misma medida
        // salían como 27 #3, 14 #3 y 23 #3, todas L= 0.44 G= 0.11 (decisión 23 del plan de #287).
        string Clave(Rebar b) => $"{b.GetTypeId()}|{Math.Round(LectorDespiece.LargoBarra(b) * 30.48)}|{Math.Round(GeneradorPrevia.Gancho(b) * 30.48)}";
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
