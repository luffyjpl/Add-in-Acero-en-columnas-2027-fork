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

    /// <summary>El corte, por lo menos así de sobre el primer estribo, para que el estribo se vea (decisión 12 del plan de #317).</summary>
    private const double SobreElEstribo = 0.03 * M;

    /// <summary>El fondo de la vista, así de bajo el primer estribo: lo de más abajo (los ganchos de anclaje) no se ve.</summary>
    private const double BajoElEstribo = 0.02 * M;

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
    private static double TextoAncho => 0.08 * M * Letra.Factor;
    private static double CotaAncho => 0.17 * M * Letra.Factor;
    private static double TextoAlto => 0.08 * M * Letra.Factor;
    private static double CotaAlto => 0.17 * M * Letra.Factor;
    private static double Leyenda => 0.32 * M * Letra.Factor;
    private static double EstriboAparte => 0.70 * M * Letra.Factor;

    /// <summary>A 1:15 la caja de una nota mide 0.12 m (medido en la viga).</summary>
    private static double FilaTexto => 0.13 * M * Letra.Factor;

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
        Element arriba = Comienzo(doc, columna, barras);
        BoundingBoxXYZ caja = arriba.get_BoundingBox(null);
        // Un mismo elemento puede bajar hasta la cimentación, donde no hay estribos y sí los ganchos de anclaje de las barras
        // (Col B-3, 09-nov): el corte va justo sobre el primer estribo y la vista no mira más abajo de él.
        List<double> estribosAbajo = barras.Where(b => !LectorColumnas.EsVertical(b) && b.GetHostId() == arriba.Id)
            .Select(b => LectorColumnas.Alturas(b).Abajo).Where(z => z >= caja.Min.Z).ToList();
        double? primerEstribo = estribosAbajo.Count > 0 ? estribosAbajo.Min() : null;
        double zCorte = Corte(columna, caja.Min.Z), zFondo = caja.Min.Z + SobreElFondo;
        if (primerEstribo is { } e0)
        {
            zCorte = Math.Max(zCorte, e0 + SobreElEstribo);
            zFondo = Math.Max(zFondo, e0 - BajoElEstribo);
        }
        XYZ centro = new((caja.Min.X + caja.Max.X) / 2, (caja.Min.Y + caja.Max.Y) / 2, zCorte);
        double ancho = caja.Max.X - caja.Min.X, alto = caja.Max.Y - caja.Min.Y;
        XYZ En(double x, double y) => new(x, y, zCorte);

        ViewPlan vista = Planta(doc, zCorte, zFondo);
        Registro.Paso($"sección de columna: primer estribo a {(primerEstribo is { } p ? (p * 0.3048).ToString("0.00") : "—")}, fondo de la vista a {zFondo * 0.3048:0.00}");
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
        var desplazamientos = new List<(XYZ Real, XYZ Corrimiento)>();
        Dictionary<ElementId, XYZ> corridas = rectangular ? Corridas(doc, columna, cruzan, centro, desplazamientos) : [];
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
        if (!porTraslapo && desplazamientos.Count > 0)
            GrapasCorridas(doc, vista, barras, desplazamientos, zFondo, zCorte);
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
        // La de traslapos va sin estribos ni menores (decisión 20 del plan de #317): solo las barras en sus dos colores. Se
        // ocultan después de acotar, que mide el recubrimiento con el estribo.
        if (porTraslapo)
        {
            List<ElementId> transversales = barras.Where(b => !LectorColumnas.EsVertical(b)).Select(b => b.Id).Where(id => doc.GetElement(id).CanBeHidden(vista)).ToList();
            if (transversales.Count > 0)
                vista.HideElements(transversales);
        }
        // El encabezado del estribo aparte (#313) sube más que la columna: el recorte de arriba crece con él.
        double sobre = alto / 2 + 0.10 * M * Letra.Factor;
        if (estribo is not null && !porTraslapo)
            sobre = Math.Max(sobre, SacarEstribo(doc, vista, barras, estribo, En(caja.Max.X + EstriboAparte, centro.Y), tipos) - centro.Y + 0.05 * M * Letra.Factor);
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
        // Los estribos y menores, en el color de su diámetro, si ese diámetro no está ya (decisión 15 del plan de #317).
        foreach (string nombre in barras.Where(b => !LectorColumnas.EsVertical(b)).Select(b => TextosDespiece.Nombre(doc.GetElement(b.GetTypeId()).Name))
            .Distinct().OrderByDescending(TextosDespiece.Numero))
        {
            if (!porTraslapo && !leyenda.Any(l => l.Texto.EndsWith(" " + nombre, StringComparison.Ordinal)))
            {
                (byte r, byte v, byte a) = ColoresDiametro.Rgb(TextosDespiece.Numero(nombre));
                leyenda.Add((nombre, new Color(r, v, a)));
            }
        }
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
            alto / 2 + Leyenda + Math.Max(0, leyenda.Count - 1) * FilaTexto + 0.15 * M * Letra.Factor, sobre);
        Registro.Paso("sección de columna: leyenda " + string.Join(", ", leyenda.Select(l => l.Texto)));
        doc.Regenerate();
        return vista;
    }

    /// <summary>
    /// Las barras cortadas que no están en su lugar, con el lugar donde van (decisiones 34 y 35 del plan de #287). El lugar
    /// sale de <see cref="PosicionesIdeales"/>: las esquinas tocan las dos caras y las de cada cara se reparten entre sus
    /// esquinas. Solo en columnas rectangulares.
    /// </summary>
    private static Dictionary<ElementId, XYZ> Corridas(Document doc, ColumnaLeida columna, HashSet<ElementId> cortadas, XYZ centro, List<(XYZ Real, XYZ Corrimiento)> desplazamientos)
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
            // Todas al lugar de la configuración, también la única pieza de una varilla: el add-in de columnas aparta del
            // gancho del estribo la barra de esa esquina, y Luciano quiere verla como la configuró (decisión 29 del plan de
            // #317, que deshace la 19). Las grapas que la abrazan se corren con ella (GrapasCorridas).
            foreach ((ElementId barra, XYZ punto) in varillas[i].Where(p => cortadas.Contains(p.Barra)))
            {
                if (Math.Abs(punto.X - ideales[i].X) > Igual || Math.Abs(punto.Y - ideales[i].Y) > Igual)
                {
                    corridas[barra] = new XYZ(ideales[i].X, ideales[i].Y, punto.Z);
                    desplazamientos.Add((punto, new XYZ(ideales[i].X - punto.X, ideales[i].Y - punto.Y, 0)));
                }
            }
        }
        return corridas;
    }

    /// <summary>
    /// Las grapas que abrazan una barra corrida a su lugar se dibujan corridas con ella (decisión 29 del plan de #317): se
    /// oculta la grapa y va su misma forma en líneas de detalle, con cada punta corrida lo mismo que la barra que abraza y
    /// lo del medio repartido entre las dos, para que siga recta. Solo las que se ven entre el fondo y el corte.
    /// </summary>
    private static void GrapasCorridas(Document doc, ViewPlan vista, List<Rebar> barras, List<(XYZ Real, XYZ Corrimiento)> desplazamientos, double zFondo, double zCorte)
    {
        double z = vista.GenLevel?.Elevation ?? zCorte;
        XYZ Corrimiento(XYZ p) => desplazamientos.Where(d => Math.Sqrt(Math.Pow(d.Real.X - p.X, 2) + Math.Pow(d.Real.Y - p.Y, 2)) < CercaDeLaBarra)
            .Select(d => d.Corrimiento).FirstOrDefault() ?? XYZ.Zero;
        var ocultar = new List<ElementId>();
        int dibujadas = 0;
        foreach (Rebar b in barras.Where(b => !LectorColumnas.EsVertical(b) && GeneradorPrevia.Curvas(b, true).Count <= 2))
        {
            bool corrida = false;
            var lineas = new List<Curve>();
            for (int i = 0; i < b.NumberOfBarPositions; i++)
            {
                List<XYZ> puntos = b.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, i)
                    .SelectMany(c => c.Tessellate()).ToList();
                if (puntos.Count < 2 || puntos.Average(p => p.Z) is var alto && (alto < zFondo || alto > zCorte))
                    continue;
                XYZ a = puntos[0], e = puntos[^1], da = Corrimiento(a), de = Corrimiento(e);
                if (da.IsZeroLength() && de.IsZeroLength())
                    continue;
                corrida = true;
                XYZ eje = new XYZ(e.X - a.X, e.Y - a.Y, 0);
                double largo2 = Math.Max(eje.DotProduct(eje), 1e-9);
                List<XYZ> corridos = puntos.Select(p =>
                {
                    double t = Math.Clamp(new XYZ(p.X - a.X, p.Y - a.Y, 0).DotProduct(eje) / largo2, 0, 1);
                    XYZ d = da * (1 - t) + de * t;
                    return new XYZ(p.X + d.X, p.Y + d.Y, z);
                }).ToList();
                for (int k = 1; k < corridos.Count; k++)
                    if (corridos[k].DistanceTo(corridos[k - 1]) > doc.Application.ShortCurveTolerance)
                        lineas.Add(Line.CreateBound(corridos[k - 1], corridos[k]));
            }
            if (!corrida || !b.CanBeHidden(vista))
                continue;
            ocultar.Add(b.Id);
            foreach (Curve l in lineas)
            {
                DetailCurve linea = doc.Create.NewDetailCurve(vista, l);
                GeneradorPrevia.Pintar(vista, linea.Id, b, GrosorGrapa);
            }
            dibujadas++;
        }
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
        Registro.Paso($"sección de columna: {dibujadas} grapas corridas con sus barras");
    }

    /// <summary>Una punta de grapa a menos de esto del centro de una barra la abraza (radio de la barra más el doblez).</summary>
    private const double CercaDeLaBarra = 0.08 * M;

    /// <summary>El grosor de línea de una grapa dibujada a mano, para que se parezca a la barra de Revit.</summary>
    private const int GrosorGrapa = 6;

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
    /// común a toda la pila, y con estribos (decisión 10 del plan de #317: el tramo contra la zapata no los lleva, y ahí la
    /// sección salía sin estribo y con los ganchos de anclaje). Un dado más ancho abajo no cuenta. Si ninguno tiene
    /// estribos, el más bajo con la sección; si ninguno la tiene, el más bajo.
    /// </summary>
    private static Element Comienzo(Document doc, ColumnaLeida columna, List<Rebar> barras)
    {
        double ancho = columna.Elemento.AnchoMm / 304.8, alto = columna.Elemento.AltoMm / 304.8;
        List<Element> pila = columna.Elementos.Select(doc.GetElement).Where(e => e.get_BoundingBox(null) is not null)
            .OrderBy(e => e.get_BoundingBox(null).Min.Z).ToList();
        List<Element> conSeccion = pila.Where(e =>
        {
            BoundingBoxXYZ c = e.get_BoundingBox(null);
            return Math.Abs(c.Max.X - c.Min.X - ancho) < AlCentimetro && Math.Abs(c.Max.Y - c.Min.Y - alto) < AlCentimetro;
        }).ToList();
        var conEstribos = new HashSet<ElementId>(barras.Where(b => !LectorColumnas.EsVertical(b)).Select(b => b.GetHostId()));
        Element elegido = conSeccion.FirstOrDefault(e => conEstribos.Contains(e.Id)) ?? conSeccion.FirstOrDefault() ?? pila.First();
        Registro.Paso($"sección de columna: comienzo en {elegido.Id}{(conEstribos.Contains(elegido.Id) ? "" : " (sin estribos)")}");
        return elegido;
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

    internal static void Pintar(View vista, ElementId id, Color color, bool siempre = false)
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
            // Como cota en cadena contra planos de referencia, como en la viga (#313); si Revit no la acepta, en texto.
            if (!GeneradorPrevia.CadenaPorPlanos(doc, vista, "sección de columna: cadena del ancho", new XYZ(x0, y0 - TextoAncho, zCorte), XYZ.BasisX,
                    [0, ex0 - x0, ex1 - x0, x1 - x0], tipos.Cota))
                TextNote.Create(doc, vista.Id, new XYZ((x0 + x1) / 2, y0 - TextoAncho, zCorte),
                    GeneradorPrevia.Cadena(ex0 - x0, ex1 - ex0, x1 - ex1), tipos.Nota(HorizontalTextAlignment.Center));
            if (!GeneradorPrevia.CadenaPorPlanos(doc, vista, "sección de columna: cadena del alto", new XYZ(x0 - TextoAlto, y0, zCorte), XYZ.BasisY,
                    [0, ey0 - y0, ey1 - y0, y1 - y0], tipos.Cota))
            {
                TextNoteOptions vertical = tipos.Nota(HorizontalTextAlignment.Center);
                vertical.Rotation = Math.PI / 2;
                TextNote.Create(doc, vista.Id, new XYZ(x0 - TextoAlto, (y0 + y1) / 2, zCorte), GeneradorPrevia.Cadena(ey0 - y0, ey1 - ey0, y1 - ey1), vertical);
            }
        }
        catch (Exception e)
        {
            // Mejor sin cotas que sin sección.
            Registro.Paso("sección de columna: cotas no creadas: " + e.Message);
        }
    }

    /// <summary>
    /// Cada forma de estribo aparte, a la derecha, con sus medidas (rotación 0: queda igual que en la sección, hallazgo
    /// 21). Primero el principal y después los menores (grapas, cajas), apilados hacia abajo con
    /// <see cref="GeneradorPrevia.ApilarEstribos"/>, igual que en la viga (decisión 18). Devuelve la Y más alta que ocupa.
    /// </summary>
    private static double SacarEstribo(Document doc, View vista, List<Rebar> barras, Rebar principal, XYZ punto, TiposPropios tipos)
    {
        // Por tipo, largo y gancho al centímetro, no por la forma de Revit (decisión 23 del plan de #287), y la grapa de los
        // traslapos sumada a la normal (decisión 16 del plan de #317).
        List<List<Rebar>> formas = GeneradorPrevia.FormasEstribos(barras.Where(b => !LectorColumnas.EsVertical(b)).ToList(), principal);
        // Cerrado, con el encabezado arriba y el diámetro abajo, igual que en la viga (#313).
        return GeneradorPrevia.ApilarEstribos(doc, vista, formas, principal, punto, tipos).Techo;
    }
}
