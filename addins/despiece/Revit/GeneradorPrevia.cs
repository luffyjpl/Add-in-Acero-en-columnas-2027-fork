using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Despiece.Calculo;
using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Despiece.Revit;

/// <summary>
/// Las imágenes de la vista previa. <paramref name="SeccionVarillas"/>: solo en la columna, la segunda sección superior
/// (con los colores de cada varilla); en ella <paramref name="Seccion"/> es la de los colores de traslapo (decisión 16).
/// </summary>
internal sealed record PreviaGenerada(string Longitudinal, string Seccion, object? Capa = null, string SeccionVarillas = "");

/// <summary>
/// Lo que se puede mover en la vista larga (Juan, 31-oct). <paramref name="Movimientos"/> va por pieza, en metros.
/// <paramref name="Separar"/>: la imagen sale sin barras sacadas ni sus textos, y se entregan para que los dibuje la página.
/// </summary>
internal sealed record DespieceMovible(IReadOnlyDictionary<string, Movimiento> Movimientos, bool Separar)
{
    public static DespieceMovible Fijo { get; } = new(new Dictionary<string, Movimiento>(), false);

    public List<ElementId> Ocultos { get; } = [];

    public List<PiezaMovible> Piezas { get; } = [];

    /// <summary>Textos con flechas a piezas (el <c>2x piel</c>): la página mueve la punta de cada flecha con su pieza.</summary>
    public List<GuiaMovible> Guias { get; } = [];

    public (PuntoModelo A, PuntoModelo B)? Marcas { get; set; }

    /// <summary>Las cotas que se arrastran enteras (paso 10 del plan del 10-11).</summary>
    public List<CotaMovible> Cotas { get; } = [];
}

/// <summary>
/// Una cota que se arrastra entera, con la clave de su movimiento (paso 10 del plan docs/planes/2026-11-10-despiece-santiago-14-15.md):
/// sus líneas (la de cota y las de referencia), dónde van sus rayitas y sus números, en coordenadas de la vista, en pies.
/// </summary>
internal sealed record CotaMovible(string Id, List<(PuntoModelo A, PuntoModelo B)> Lineas, List<PuntoModelo> Marcas, List<TextoMovible> Textos);

/// <summary>Lo que se creó en cada grupo de cotas de la vista larga: se mide y se oculta para que la página lo arrastre.</summary>
internal sealed class CotasCreadas
{
    public List<Element> Abajo { get; } = [];

    public List<Element> Arriba { get; } = [];
}

/// <summary>Un texto de la capa: su centro en coordenadas de la vista, en pies.</summary>
internal sealed record TextoMovible(string Texto, PuntoModelo Centro, bool Girado);

/// <summary>Un texto fijo con una flecha a cada pieza de <paramref name="Puntas"/>; <paramref name="Desde"/> es donde salen.</summary>
internal sealed record GuiaMovible(TextoMovible Texto, PuntoModelo Desde, List<(string Pieza, PuntoModelo Punta)> Puntas);

/// <summary>Una pieza de la capa. Sin <paramref name="Puntos"/>, es un texto suelto, como la etiqueta de unos estribos.</summary>
internal sealed record PiezaMovible(string Id, string Color, List<PuntoModelo> Puntos, TextoMovible Etiqueta, List<TextoMovible> Anexos);

/// <summary>Las vistas del navegador de Revit. <paramref name="YaExistian"/>: no se creó nada porque ya estaban.</summary>
internal sealed record VistasCreadas(string Longitudinal, string Seccion, bool YaExistian);

/// <summary>
/// La vista previa real (#221): crea en Revit la vista longitudinal y la sección de una viga como lo hace Santiago, las
/// exporta y deshace todo. Los pasos y sus motivos están en "Cómo se hace" de #217. No usa nombres de parámetros ni de
/// formas, que cambian con el idioma de Revit.
/// </summary>
internal static class GeneradorPrevia
{
    private const double M = 1 / 0.3048;

    /// <summary>La escala la elige el tablero (#272); antes era 1:15 fija (Juan, 30-oct).</summary>
    private static int EscalaLongitudinal => Letra.EscalaAlzado;
    private static int EscalaSeccion => Letra.EscalaSeccion;

    // Las separaciones que dependen del texto se midieron con letra de 2.5 mm a 1:15 y crecen con la letra y la escala
    // elegidas en el tablero (Letra.Factor, #261 y #272); el aire entre dibujos crece solo con la escala (Letra.Papel);
    // las demás son del modelo y no cambian.
    // Las separaciones de la vista larga se midieron a 1:50, en pies del modelo; el texto ocupa lo que dice la escala,
    // así que se reducen en proporción para que la vista no quede llena de aire.
    private const double Proporcion = EscalaVista.Base / 50.0;
    // A 1:15 la caja de la nota baja 0.09 m desde su punto (medido en V 3, 30-oct); el resto es aire sobre la barra.
    private static double AltoTexto => 0.08 * M * Letra.Factor;
    private const double MinimoGancho = 0.05 * M;
    private const double MinimoTraslapo = 0.01 * M;
    private static double MedioAnchoEtiqueta => 0.65 * M * Proporcion * Letra.Factor;
    private static double HuecoFilas => 0.08 * M * Proporcion * Letra.Factor;
    private static double HuecoEnFila => 0.20 * M * Proporcion * Letra.Factor;
    private static double DesfaseTraslapo => 0.02 * M * Letra.Papel;
    private const double MismaZona = 0.10 * M;
    // La caja de la nota mide 0.12 m de alto a 1:15 (medido en V 3, 30-oct).
    private static double FilaEstribos => 0.13 * M * Letra.Factor;
    /// <summary>
    /// Entre dos filas de etiquetas de estribos sobre la viga: casi el alto de la letra (decisión 41 del plan de #317; con
    /// FilaEstribos quedaban muy separadas y empujaban hacia arriba las barras).
    /// </summary>
    private static double FilaEtiquetasEstribos => 0.06 * M * Letra.Factor;
    /// <summary>
    /// Medio ancho de un texto de una línea en la vista larga: 0.65 del alto de la letra por carácter. Medido en la captura
    /// de V C del 10-11 (letra 2.0 mm, 1:50): `3 #7 L= 2.60/3.00` mide 1.09 m (0.64 por carácter); con 0.55 la medida del
    /// traslapo quedaba pegada a la etiqueta.
    /// </summary>
    private static double MedioAnchoTexto(string texto) => texto.Length * 0.65 * Letra.Mm / 1000 * EscalaLongitudinal * M / 2;

    private static double AireNota => 0.01 * M * Letra.Papel;
    private static double AnchoNotaGirada => 0.10 * M * Letra.Factor;

    // Bajo la sección y a su izquierda, como en la columna (#282, #313): primero (más cerca) la cadena del recubrimiento
    // `0.04 | 0.27 | 0.04`, después la cota total; debajo de todo, la leyenda. Ya no hay etiquetas con flechas (#313).
    private static double CadenaSeccion => 0.06 * M * Letra.Factor;
    private static double TotalSeccion => 0.12 * M * Letra.Factor;
    private static double LeyendaSeccion => 0.30 * M * Letra.Factor;
    private static double FilaLeyenda => 0.13 * M * Letra.Factor;
    private static double AnchoCuadro => 0.09 * M * Letra.Factor;
    /// <summary>El <c>x= 1.50</c> sobre la sección (#314): el punto de la nota, sobre la cara de arriba.</summary>
    private static double SobreSeccion => 0.20 * M * Letra.Factor;
    /// <summary>El encabezado del estribo aparte, tres líneas (<see cref="TextosDespiece.EncabezadoEstribo"/>).</summary>
    private static double AltoEncabezado => 0.30 * M * Letra.Factor;

    // La vista es de la viga y las columnas de sus nudos, sin el acero de ellas (#262, Juan 05-nov). El recorte va al alto
    // de la viga más el asomo de las columnas; las barras sacadas se ven por fuera de él.
    private const double HolguraRecorte = 0.005 * M;
    // Lo que se ve de las columnas arriba y abajo de la viga (#262, decisión 9 del plan de #266): un poco, como las vigas en
    // la vista de columna. Las etiquetas de estribos y las barras sacadas empiezan después.
    private const double Asomo = 0.20 * M;
    private static double ColaEje => 0.15 * M * Proporcion * Letra.Papel;
    private static double CabezaEje => 0.4 * M * Proporcion * Letra.Papel;
    // Las cotas (#264, opción A de la maqueta): abajo una sola línea, con las caras de columnas y luces libres escritas
    // arriba de ella y los ejes debajo; arriba de todo, el largo completo de la viga.
    // De lo más bajo del dibujo a la línea (sus números van arriba, como antes la fila de luces).
    private static double CotaLinea => 0.20 * M * Proporcion * Letra.Factor;
    // Debajo de la línea: los números de los ejes y la nota del total entre ejes.
    private static double BajoLinea => 0.25 * M * Proporcion * Letra.Factor;
    // De lo más alto (las barras de arriba) a la cota del largo de la viga.
    private static double CotaArriba => 0.20 * M * Proporcion * Letra.Factor;
    private const double AlturaPlano = 0.03 * M;

    /// <summary>Un bastón va más adentro que la capa exterior por más de esto.</summary>
    private const double ToleranciaBaston = 0.005 * M;

    /// <summary>Aire entre un estribo con su texto y el siguiente que se apila debajo.</summary>
    private static double HuecoEstribos => 0.08 * M * Letra.Papel;

    /// <summary>
    /// Textos en negro en vez del color de su diámetro (Juan, 31-oct). Es un dato de esta pasada, no del modelo: lo lee
    /// <see cref="Pintar"/> para no repasarlo por cada una de las notas. Solo vale dentro de <see cref="Generar"/> y <see cref="Crear"/>.
    /// </summary>
    [ThreadStatic]
    internal static bool textosNegros;

    public static PreviaGenerada Generar(Document doc, VigaContinua viga, string carpeta, bool negros, DespieceMovible movible)
    {
        Registro.Paso("previa: empieza " + viga.Elemento.Id);
        using var t = new Transaction(doc, "Vista previa de despiece");
        t.Start();
        textosNegros = negros;
        try
        {
            return Producir(doc, viga, carpeta, movible);
        }
        finally
        {
            textosNegros = false;
            Registro.Paso("previa: deshaciendo");
            // La vista previa no deja nada en el modelo: los errores de Revit se descartan sin mostrarlos (decisión 33 de #287).
            t.RollBack(t.GetFailureHandlingOptions().SetClearAfterRollback(true));
            Registro.Paso("previa: deshecho");
        }
    }

    /// <summary>Crea y exporta las dos vistas. Quien llama abre la transacción y la deshace.</summary>
    private static PreviaGenerada Producir(Document doc, VigaContinua viga, string carpeta, DespieceMovible movible)
    {
        (View longitudinal, View seccion) = Construir(doc, viga, TiposPropios.Crear(doc, false), movible);
        Registro.Paso("previa: exportando la longitudinal");
        string a = ExportadorImagen.Exportar(doc, longitudinal, carpeta, ExportadorImagen.AnchoLarga);
        object? capa = null;
        if (movible.Marcas is { } marcas)
        {
            var (calibracion, _, _) = ExportadorImagen.Calibrar(a, marcas.A, marcas.B);
            if (calibracion is not null)
                capa = Capa(movible, ConEscalaIgual(calibracion, (marcas.A.Y + marcas.B.Y) / 2));
            else
            {
                // Mejor fija que mal alineada: la imagen completa, sin arrastre (decisión 6 del plan).
                Registro.Paso("previa: sin calibración, la vista larga queda fija");
                longitudinal.UnhideElements(movible.Ocultos);
                doc.Regenerate();
                a = ExportadorImagen.Exportar(doc, longitudinal, carpeta, ExportadorImagen.AnchoLarga);
            }
        }
        Registro.Paso("previa: exportando la sección");
        string b = ExportadorImagen.Exportar(doc, seccion, carpeta);
        return new PreviaGenerada(a, b, capa);
    }

    /// <summary>
    /// Las cruces están muy lejos a lo largo de la viga y muy cerca en altura. La imagen tiene la misma escala en los dos
    /// sentidos, así que la vertical se toma de la horizontal, que es la precisa, sin mover la altura media de las cruces.
    /// </summary>
    private static Calibracion ConEscalaIgual(Calibracion c, double alturaMedia)
    {
        double escalaY = -Math.Abs(c.EscalaX);
        return c with { EscalaY = escalaY, OrigenY = c.OrigenY + alturaMedia * (c.EscalaY - escalaY) };
    }

    /// <summary>
    /// Las piezas en píxeles de la imagen, y cuántos píxeles mide un metro, para arrastrarlas en metros.
    /// <paramref name="horizontal"/>: las barras se arrastran a los lados (columna) y no arriba y abajo (viga).
    /// </summary>
    internal static object Capa(DespieceMovible movible, Calibracion c, bool horizontal = false)
    {
        object Texto(TextoMovible t)
        {
            (double x, double y) = c.APixel(t.Centro);
            return new { texto = t.Texto, x, y, girado = t.Girado };
        }
        return new
        {
            escala = Math.Abs(c.EscalaX) / 0.3048,
            horizontal,
            // Revit mide la altura de la mayúscula; en la página se pide el tamaño de la fuente, y en Arial la mayúscula es el 72 %.
            // Lo movible es siempre de la vista larga o del alzado: con su escala, aunque después se haya armado la sección.
            altoLetra = Math.Abs(c.EscalaX) / 0.3048 * Letra.Mm / 1000 * EscalaLongitudinal / 0.72,
            piezas = movible.Piezas.Select(p => new
            {
                id = p.Id,
                color = p.Color,
                puntos = p.Puntos.Select(q =>
                {
                    (double x, double y) = c.APixel(q);
                    return new[] { x, y };
                }),
                etiqueta = Texto(p.Etiqueta),
                anexos = p.Anexos.Select(Texto),
            }),
            cotas = movible.Cotas.Select(k => new
            {
                id = k.Id,
                lineas = k.Lineas.Select(l =>
                {
                    (double x1, double y1) = c.APixel(l.A);
                    (double x2, double y2) = c.APixel(l.B);
                    return new[] { x1, y1, x2, y2 };
                }),
                marcas = k.Marcas.Select(q =>
                {
                    (double x, double y) = c.APixel(q);
                    return new[] { x, y };
                }),
                textos = k.Textos.Select(Texto),
            }),
            guias = movible.Guias.Select(g =>
            {
                (double x, double y) = c.APixel(g.Desde);
                return new
                {
                    texto = Texto(g.Texto),
                    desde = new[] { x, y },
                    puntas = g.Puntas.Select(p =>
                    {
                        (double px, double py) = c.APixel(p.Punta);
                        return new { pieza = p.Pieza, x = px, y = py };
                    }),
                };
            }),
        };
    }

    /// <summary>
    /// Deja las dos vistas en el navegador de Revit (#222), iguales a la vista previa. No coloca nada en las hojas. Si ya
    /// existe alguna, no crea nada: reemplazarla podría borrar lo que se haya cambiado a mano.
    /// </summary>
    public static VistasCreadas Crear(Document doc, VigaContinua viga, string nombre, bool negros, DespieceMovible movible)
    {
        string nombreSeccion = "Sección " + nombre;
        bool yaExisten = new FilteredElementCollector(doc).OfClass(typeof(ViewSection)).Any(v => v.Name == nombre || v.Name == nombreSeccion);
        if (yaExisten)
            return new VistasCreadas(nombre, nombreSeccion, true);

        using var t = new Transaction(doc, "Crear vistas de despiece");
        t.Start();
        textosNegros = negros;
        try
        {
            (View longitudinal, View seccion) = Construir(doc, viga, TiposPropios.Crear(doc, true), movible);
            longitudinal.Name = nombre;
            seccion.Name = nombreSeccion;
            if (t.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("Revit no aceptó las vistas.");
            return new VistasCreadas(nombre, nombreSeccion, false);
        }
        finally
        {
            textosNegros = false;
            if (t.HasStarted() && !t.HasEnded())
                t.RollBack();
        }
    }

    /// <summary>Una de las vistas que dejó <see cref="Crear"/> para este nombre: la larga o la sección. Null si no existe.</summary>
    public static View? VistaCreada(Document doc, string nombre, bool seccion = false)
    {
        string buscado = seccion ? "Sección " + nombre : nombre;
        return new FilteredElementCollector(doc).OfClass(typeof(ViewSection)).Cast<View>().FirstOrDefault(v => !v.IsTemplate && v.Name == buscado);
    }

    private static (View Longitudinal, View Seccion) Construir(Document doc, VigaContinua viga, TiposPropios tipos, DespieceMovible movible)
    {
        ViewFamilyType tipoSeccion = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(v => v.ViewFamily == ViewFamily.Section);
        List<Rebar> barras = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>()
            .Where(b => viga.Vigas.Contains(b.GetHostId())).ToList();
        Registro.Paso($"vistas: {barras.Count} barras; vista longitudinal");
        View longitudinal = Longitudinal(doc, tipoSeccion, viga, barras, tipos, movible);
        Registro.Paso("vistas: sección");
        View seccion = Seccion(doc, tipoSeccion, viga, barras, tipos);
        doc.Regenerate();
        return (longitudinal, seccion);
    }

    private static (XYZ Min, XYZ Max) Caja(Document doc, VigaContinua viga)
    {
        List<BoundingBoxXYZ> cajas = viga.Vigas.Select(id => doc.GetElement(id).get_BoundingBox(null)).ToList();
        return (new XYZ(cajas.Min(c => c.Min.X), cajas.Min(c => c.Min.Y), cajas.Min(c => c.Min.Z)),
            new XYZ(cajas.Max(c => c.Max.X), cajas.Max(c => c.Max.Y), cajas.Max(c => c.Max.Z)));
    }

    /// <summary>Una barra sacada y lo que ocupa en la vista, medido en Revit.</summary>
    private sealed record Sacada(Rebar Barra, Element Detalle, XYZ Posicion, int Cantidad, double Izquierda, double Derecha, double Abajo, double Arriba)
    {
        public double Alto => Arriba - Abajo;

        /// <summary>Dónde quedó el detalle. Su etiqueta y sus medidas se mueven con él.</summary>
        public XYZ? Final { get; set; }

        public TextNote? Etiqueta { get; set; }

        /// <summary>En la piel, de qué cara de la viga es (0 o 1): cada cara va en su línea, con sus traslapos.</summary>
        public int Cara { get; init; }

        public List<TextNote> Anexos { get; } = [];

        private double GanchoArriba => Barra.get_BoundingBox(null).Max.Z - Mayor(Barra).GetEndPoint(0).Z;

        private double GanchoAbajo => Mayor(Barra).GetEndPoint(0).Z - Barra.get_BoundingBox(null).Min.Z;

        /// <summary>
        /// Del borde inferior del detalle a su tramo recto. Revit pone la medida del tramo al lado contrario del gancho: con
        /// el gancho arriba o sin gancho va debajo; con el gancho abajo, encima (visto en V 3).
        /// </summary>
        public double Linea => GanchoAbajo > MinimoGancho ? GanchoAbajo : GanchoArriba > MinimoGancho ? Alto - GanchoArriba : Alto;

        /// <summary>Lo que ocupa la medida del tramo por encima de él: solo si el gancho va abajo.</summary>
        public double CotaEncima => GanchoAbajo > MinimoGancho ? Alto - GanchoAbajo : 0;
    }

    private static View Longitudinal(Document doc, ViewFamilyType tipo, VigaContinua viga, List<Rebar> barras, TiposPropios tipos, DespieceMovible movible)
    {
        Letra.EnAlzado();
        (XYZ min, XYZ max) = Caja(doc, viga);
        XYZ derecha = SentidoDeLectura(doc, viga);
        // Al crear la sección, la derecha de la vista sale contraria al BasisX pedido (medido el 30-oct: con +X salió C-B-A).
        XYZ basisX = derecha.Negate();
        XYZ haciaElQueMira = basisX.CrossProduct(XYZ.BasisZ);
        XYZ centro = (min + max) / 2;
        double largo = Math.Abs((max - min).DotProduct(derecha));
        double ancho = Math.Abs((max - min).DotProduct(haciaElQueMira));
        double alto = max.Z - min.Z;
        XYZ enPlano = centro + haciaElQueMira * (ancho / 2 + 0.3 * M);

        Transform t = Transform.Identity;
        t.BasisX = basisX;
        t.BasisY = XYZ.BasisZ;
        t.BasisZ = haciaElQueMira;
        t.Origin = enPlano;
        var caja = new BoundingBoxXYZ
        {
            Transform = t,
            Min = new XYZ(-largo / 2 - 1.0 * M, -alto / 2 - 12.0 * M, -ancho - 0.7 * M),
            Max = new XYZ(largo / 2 + 1.0 * M, alto / 2 + 12.0 * M, 0),
        };
        ViewSection vista = Preparar(doc, tipo, caja, "V", EscalaLongitudinal);
        Registro.Paso("longitudinal: limpiando");
        // Con las columnas de sus nudos, sin su acero (#262, Juan 05-nov; antes no salían, regla de #217 del 30-oct).
        List<ElementId> apoyos = Apoyos(doc, viga, derecha, min, max).Select(e => e.Id).ToList();
        double asomo = apoyos.Count > 0 ? Asomo : 0;
        Registro.Paso($"longitudinal: {apoyos.Count} apoyos visibles: {string.Join(", ", apoyos.Select(id => id.Value))}");
        Limpiar(doc, vista, viga, barras, apoyos);
        Registro.Paso("longitudinal: coloreando");
        Colorear(doc, vista, barras);
        SoloContorno(vista, apoyos);
        ContornoCompleto(doc, vista, viga, derecha, enPlano, min, max, apoyos);
        doc.Regenerate();

        // Se sacan todas lejos de la viga, se mide cuánto ocupa cada una y se acomodan en filas sin encimarse.
        Registro.Paso("longitudinal: sacando barras");
        List<Rebar> longitudinales = barras.Where(b => EsLongitudinal(b, derecha)).ToList();
        var creadas = new List<(Rebar Barra, Element Detalle, XYZ Posicion, int Cantidad, int Cara)>();
        // Una misma pieza puede ser varios conjuntos (S1: 3 #5 y dos sueltas #4). Las de igual diámetro y largo se dibujan
        // una vez, con su cantidad total: para figurar el acero son la misma barra. Las hechas a mano no se juntan.
        foreach (IGrouping<string, Rebar> iguales in longitudinales.GroupBy(b => MarcaVigas.Pieza(b) is { } pieza
            ? $"{pieza}|{b.GetTypeId()}|{Math.Round(LectorDespiece.LargoBarra(b) * 304.8)}"
            : b.Id.ToString()))
        {
            List<(Rebar Barra, int Cantidad)> piezas = MarcaVigas.EsPiel(iguales.First()) ? PorCara(iguales.ToList()) : [(iguales.First(), iguales.Sum(x => x.Quantity))];
            for (int cara = 0; cara < piezas.Count; cara++)
            {
                (Rebar b, int cantidad) = piezas[cara];
                XYZ c = Centro(b);
                XYZ p = new XYZ(c.X, c.Y, max.Z + 11 * M) + haciaElQueMira * (enPlano - c).DotProduct(haciaElQueMira);
                Registro.Paso($"longitudinal: barra {b.Id}");
                Element d = RebarBendingDetail.Create(doc, vista.Id, b.Id, 0, tipos.BarraSinMedidas, p, 0);
                Pintar(vista, d.Id, b, 2);
                creadas.Add((b, d, p, cantidad, cara));
            }
        }
        doc.Regenerate();
        List<Sacada> sacadas = creadas.Select(c =>
        {
            BoundingBoxXYZ m = c.Detalle.get_BoundingBox(vista);
            double a = m.Min.DotProduct(derecha), b = m.Max.DotProduct(derecha);
            return new Sacada(c.Barra, c.Detalle, c.Posicion, c.Cantidad, Math.Min(a, b), Math.Max(a, b), m.Min.Z - c.Posicion.Z, m.Max.Z - c.Posicion.Z) { Cara = c.Cara };
        }).ToList();

        double mitad = (min.Z + max.Z) / 2;
        // Arriba o abajo según la marca del add-in de vigas: S e I son las líneas de arriba y de abajo, BS y BI sus bastones.
        // Sin marca (modelada a mano), según la altura de su tramo recto, no de la caja de toda la barra: un gancho hacia
        // el centro de la viga corre el centro de la caja y mandaba la barra de arriba abajo (visto en V 3 y V A, 30-oct).
        bool EstaArriba(Sacada s) => MarcaVigas.Serie(s.Barra) switch
        {
            "S" or "BS" => true,
            // Toda la piel junta, debajo de la viga (#315, decisión 1 del plan de #317).
            "I" or "BI" or "P" => false,
            _ => Mayor(s.Barra).GetEndPoint(0).Z > mitad,
        };
        // Con marca del add-in de vigas, los bastones son BS y BI; sin marca, una barra recta que va más adentro que la capa
        // exterior de su lado.
        List<double> alturas = sacadas.Select(s => Mayor(s.Barra).GetEndPoint(0).Z).ToList();
        bool EsBaston(Sacada s) => MarcaVigas.EsBaston(s.Barra) || (MarcaVigas.Serie(s.Barra) is null && Curvas(s.Barra, false).Count == 1
            && ClasificadorBastones.EsBaston(Mayor(s.Barra).GetEndPoint(0).Z, alturas, ToleranciaBaston));
        List<Sacada> deArriba = sacadas.Where(EstaArriba).ToList(), deAbajo = sacadas.Except(deArriba).ToList();
        // La piel pegada a la viga (Luciano, 06-nov, #301); después los bastones, entre la viga y las corridas (Juan, 31-oct);
        // después una fila por diámetro, la más gruesa primero; al final, lo que quede.
        bool EsPiel(Sacada s) => MarcaVigas.EsPiel(s.Barra);
        // Una pila por lado con todo lo ya puesto: cada fila va lo más cerca de la viga sin tocar nada (paso 7 del plan del
        // 10-11; reemplaza a las decisiones 30, 39 y 42 del plan de #317, que solo miraban la fila anterior).
        double Lineas(List<Sacada> lado, string serie, Pila pila)
        {
            // La piel, una línea por cara con sus traslapos, como las corridas (decisión 43 del plan de #317): cada pedazo en su
            // fila la alejaba de la viga.
            foreach (IGrouping<int, Sacada> cara in lado.Where(EsPiel).GroupBy(s => s.Cara).OrderBy(g => g.Key))
                AcomodarLinea(doc, vista, cara.ToList(), pila, tipos);
            Acomodar(doc, vista, lado.Where(EsBaston).ToList(), pila, tipos);
            foreach (IGrouping<int, Sacada> diametro in lado.Where(s => MarcaVigas.Serie(s.Barra) == serie)
                .GroupBy(s => LectorDespiece.Numero(doc.GetElement(s.Barra.GetTypeId()).Name)).OrderByDescending(g => g.Key))
                AcomodarLinea(doc, vista, diametro.ToList(), pila, tipos);
            Acomodar(doc, vista, lado.Where(s => MarcaVigas.Serie(s.Barra) != serie && !EsBaston(s) && !EsPiel(s)).ToList(), pila, tipos);
            return pila.Z(pila.Hasta);
        }
        var sueltas = new List<(Rebar Barra, TextNote Nota)>();
        // Pegadas a la viga, no sobre el asomo de las columnas: con él quedaban 0.20 m más arriba (#302).
        EtiquetarEstribos(doc, vista, barras, derecha, enPlano, max.Z, tipos, sueltas);
        doc.Regenerate();
        var arriba = new Pila(max.Z, true, derecha);
        var abajo = new Pila(min.Z, false, derecha);
        // Lo que ya ocupa lugar: el asomo de las columnas (arriba y abajo) y las etiquetas de los estribos (arriba).
        foreach (ElementId id in apoyos)
        {
            if (doc.GetElement(id).get_BoundingBox(null) is not { } c)
                continue;
            double a = c.Min.DotProduct(derecha), b = c.Max.DotProduct(derecha);
            arriba.Obstaculo(Math.Min(a, b), Math.Max(a, b), max.Z, max.Z + asomo + HolguraRecorte);
            abajo.Obstaculo(Math.Min(a, b), Math.Max(a, b), min.Z - asomo - HolguraRecorte, min.Z);
        }
        foreach ((_, TextNote nota) in sueltas)
        {
            if (nota.get_BoundingBox(vista) is not { } c)
                continue;
            double a = c.Min.DotProduct(derecha), b = c.Max.DotProduct(derecha);
            arriba.Obstaculo(Math.Min(a, b), Math.Max(a, b), c.Min.Z, c.Max.Z);
        }
        double tope = Lineas(deArriba, "S", arriba);
        double fondo = Lineas(deAbajo, "I", abajo);

        Recortar(vista, centro, largo / 2 + 1.0 * M, largo / 2 + 1.0 * M, alto / 2 + asomo + HolguraRecorte, alto / 2 + asomo + HolguraRecorte);
        vista.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true);
        doc.Regenerate();
        double techo = Math.Max(tope, max.Z), suelo = Math.Min(fondo, min.Z);
        // Las cotas se crean ya donde el usuario las dejó en el paso 2 (paso 10 del plan del 10-11); los ejes se alargan con ellas.
        double bajaCota = Corrimiento(movible, ClaveCotaAbajo, false), subeCota = Corrimiento(movible, ClaveCotaArriba, false);
        AjustarEjes(doc, vista, techo + CotaArriba + CabezaEje + Math.Max(0, subeCota), suelo - CotaLinea - BajoLinea - ColaEje + Math.Min(0, bajaCota));
        var cotas = new CotasCreadas();
        Cotar(doc, vista, centro, derecha, largo, min, max, suelo + bajaCota, techo + subeCota, tipos, cotas);
        CotarColumnas(doc, vista, viga, centro, derecha, min, max, suelo + bajaCota, techo + subeCota, tipos, cotas);
        vista.DisplayStyle = DisplayStyle.FlatColors;
        AplicarMovimientos(doc, sacadas, sueltas, movible, derecha);
        // Después de los movimientos: las flechas van adonde quedó cada piel (decisión 27 del plan de #317).
        GuiaPiel? piel = DosVecesPiel(doc, vista, sacadas.Where(EsPiel).ToList(), derecha, tipos);
        if (movible.Separar)
            Separar(doc, vista, sacadas, sueltas, movible, derecha, enPlano, min, max, piel, cotas);
        return vista;
    }

    /// <summary>
    /// <c>2x piel</c> a la izquierda de la piel, con una flecha a cada fila (#315, referencia de Santiago): la piel va en
    /// las dos caras de la viga. Cada línea conserva su etiqueta (decisión 2 del plan de #317). Con varias piezas en una
    /// fila (piel con traslapos) la flecha va a la de la izquierda: a todas cruzaba el dibujo (decisión 27). Devuelve la nota
    /// y a qué pieza apunta cada flecha, para que en el paso de arrastrar las flechas sigan a la piel.
    /// </summary>
    private static GuiaPiel? DosVecesPiel(Document doc, View vista, List<Sacada> pieles, XYZ derecha, TiposPropios tipos)
    {
        List<Sacada> colocadas = pieles.Where(s => s.Final is not null).ToList();
        if (colocadas.Count == 0)
            return null;
        // La línea de cada pieza, como la deja ColocarFilas: Final = línea − Linea − Abajo.
        double Linea(Sacada s) => s.Final!.Z + s.Abajo + s.Linea;
        // Una flecha por cara (cada cara es una línea, decisión 43), a su pieza de la izquierda.
        List<Sacada> filas = colocadas.GroupBy(s => s.Cara).Select(g => g.OrderBy(s => s.Izquierda).First()).ToList();
        Sacada primera = colocadas.OrderBy(s => s.Izquierda).First();
        XYZ EnX(double x, double z) => new XYZ(primera.Posicion.X, primera.Posicion.Y, z) + derecha * (x - primera.Posicion.DotProduct(derecha));
        TextNote nota = TextNote.Create(doc, vista.Id, EnX(primera.Izquierda - MedioAnchoEtiqueta, filas.Average(Linea) + AltoTexto / 2), "2x piel",
            tipos.Nota(HorizontalTextAlignment.Right));
        var puntas = new List<(Sacada Pieza, XYZ Punta)>();
        foreach (Sacada s in filas)
        {
            XYZ punta = EnX(s.Izquierda + MedioAnchoEtiqueta / 2, Linea(s));
            nota.AddLeader(TextNoteLeaderTypes.TNLT_STRAIGHT_R).End = punta;
            puntas.Add((s, punta));
        }
        Pintar(vista, nota.Id, primera.Barra, null);
        // Sin esto la nota recién creada no tiene caja en la vista y Separar fallaba al medirla (NullReference, 09-nov).
        doc.Regenerate();
        Registro.Paso($"longitudinal: 2x piel con {filas.Count} flechas ({colocadas.Count} piezas)");
        return new GuiaPiel(nota, puntas);
    }

    /// <summary>El <c>2x piel</c> y sus flechas, cada una con la pieza a la que apunta.</summary>
    private sealed record GuiaPiel(TextNote Nota, List<(Sacada Pieza, XYZ Punta)> Puntas);


    /// <summary>
    /// La piel, siempre dos líneas, una por cara de la viga, cada una con la mitad de las barras (decisión 22 del plan de
    /// #317, Luciano: "5 de este lado y 5 del otro: deberían aparecer 2 barras azules que digan 5 #4"). No importa cómo
    /// estén repartidas en conjuntos de Revit.
    /// </summary>
    private static List<(Rebar Barra, int Cantidad)> PorCara(List<Rebar> piel)
    {
        int total = piel.Sum(b => b.Quantity);
        return total >= 2 ? [(piel[0], total / 2), (piel[^1], total - total / 2)] : [(piel[0], total)];
    }

    private static string ClaveEstribos(Rebar b) => "e" + b.Id.Value;

    /// <summary>Lo que el usuario movió en el paso 2. Una pieza que ya no está en el modelo simplemente no aparece aquí.</summary>
    private static void AplicarMovimientos(Document doc, List<Sacada> sacadas, List<(Rebar Barra, TextNote Nota)> sueltas, DespieceMovible movible, XYZ derecha)
    {
        foreach (Sacada s in sacadas)
        {
            if (s.Final is null || !movible.Movimientos.TryGetValue(s.Barra.Id.Value.ToString(), out Movimiento? m))
                continue;
            XYZ arriba = XYZ.BasisZ * (m.Dz / 0.3048);
            RebarBendingDetail.SetPosition(s.Detalle, s.Final + arriba);
            // Donde quedó: las flechas de la piel se ponen después y lo leen (decisión 27).
            s.Final = s.Final! + arriba;
            foreach (TextNote anexo in s.Anexos)
                anexo.Coord += arriba;
            if (s.Etiqueta is not null)
                s.Etiqueta.Coord += arriba + derecha * (m.Tx / 0.3048) + XYZ.BasisZ * (m.Tz / 0.3048);
        }
        foreach ((Rebar barra, TextNote nota) in sueltas)
        {
            if (movible.Movimientos.TryGetValue(ClaveEstribos(barra), out Movimiento? m))
                nota.Coord += derecha * (m.Tx / 0.3048) + XYZ.BasisZ * (m.Tz / 0.3048);
        }
        doc.Regenerate();
    }

    /// <summary>
    /// Mide lo movible, lo oculta de la imagen y deja las dos cruces de calibración: la página lo dibuja encima y se puede
    /// arrastrar. Una línea o un texto medidos aquí caen en el mismo sitio donde los pondría Revit.
    /// </summary>
    private static void Separar(Document doc, View vista, List<Sacada> sacadas, List<(Rebar Barra, TextNote Nota)> sueltas, DespieceMovible movible, XYZ derecha, XYZ enPlano, XYZ min, XYZ max, GuiaPiel? piel = null, CotasCreadas? cotas = null)
    {
        (double A, double B) EnX(BoundingBoxXYZ c)
        {
            double a = c.Min.DotProduct(derecha), b = c.Max.DotProduct(derecha);
            return (Math.Min(a, b), Math.Max(a, b));
        }
        TextoMovible Texto(TextNote nota, bool girado)
        {
            BoundingBoxXYZ c = nota.get_BoundingBox(vista);
            (double a, double b) = EnX(c);
            return new TextoMovible(nota.Text.Trim(), new PuntoModelo((a + b) / 2, (c.Min.Z + c.Max.Z) / 2), girado);
        }
        void Ocultar(Element e)
        {
            if (e.CanBeHidden(vista))
                movible.Ocultos.Add(e.Id);
        }

        foreach (Sacada s in sacadas.Where(x => x.Etiqueta is not null))
        {
            BoundingBoxXYZ caja = s.Detalle.get_BoundingBox(vista);
            (double izquierda, _) = EnX(caja);
            // El dibujo es la barra real trasladada: se lleva a la esquina de su detalle.
            List<XYZ> curva = [];
            foreach (Curve c in Curvas(s.Barra, false))
                curva.AddRange(c.Tessellate());
            double dx = izquierda - curva.Min(p => p.DotProduct(derecha)), dz = caja.Min.Z - curva.Min(p => p.Z);
            List<PuntoModelo> puntos = curva.Select(p => new PuntoModelo(p.DotProduct(derecha) + dx, p.Z + dz)).ToList();
            string color = LectorDespiece.Color(doc.GetElement(s.Barra.GetTypeId()).Name);
            movible.Piezas.Add(new PiezaMovible(s.Barra.Id.Value.ToString(), color, puntos, Texto(s.Etiqueta!, false),
                s.Anexos.Select(n => Texto(n, Math.Abs(n.BaseDirection.Z) > 0.5)).ToList()));
            Ocultar(s.Detalle);
            Ocultar(s.Etiqueta!);
            s.Anexos.ForEach(Ocultar);
        }
        foreach ((Rebar barra, TextNote nota) in sueltas)
        {
            movible.Piezas.Add(new PiezaMovible(ClaveEstribos(barra), LectorDespiece.Color(doc.GetElement(barra.GetTypeId()).Name), [], Texto(nota, false), []));
            Ocultar(nota);
        }
        // El 2x piel y sus flechas también los dibuja la página, para que las flechas sigan a la piel al arrastrarla
        // (decisión 27 del plan de #317). Las flechas salen del borde derecho de la nota.
        if (piel is not null)
        {
            BoundingBoxXYZ? c = piel.Nota.get_BoundingBox(vista);
            if (c is null)
            {
                Registro.Paso("longitudinal: el 2x piel no tiene caja; queda en la imagen");
                piel = null;
            }
        }
        if (piel is not null)
        {
            BoundingBoxXYZ c = piel.Nota.get_BoundingBox(vista);
            (_, double derechaNota) = EnX(c);
            movible.Guias.Add(new GuiaMovible(Texto(piel.Nota, false), new PuntoModelo(derechaNota, (c.Min.Z + c.Max.Z) / 2),
                piel.Puntas.Select(p => (p.Pieza.Barra.Id.Value.ToString(), new PuntoModelo(p.Punta.DotProduct(derecha), p.Punta.Z))).ToList()));
            Ocultar(piel.Nota);
        }
        // Las cotas de abajo (ejes y caras de columnas, en la misma línea) y la de arriba (largo y "Ejes"), cada grupo entero
        // (paso 10 del plan del 10-11). Sus líneas de referencia van hacia la viga.
        if (cotas is not null)
        {
            foreach ((string id, List<Element> elementos, double hacia) in new[] { (ClaveCotaAbajo, cotas.Abajo, 1.0), (ClaveCotaArriba, cotas.Arriba, -1.0) })
            {
                var cota = new CotaMovible(id, [], [], []);
                foreach (Element e in elementos)
                {
                    if (e is Dimension d)
                        MedirCota(d, cota, derecha, derecha, XYZ.BasisZ * hacia * LineaReferencia, false);
                    else if (e is TextNote n)
                        cota.Textos.Add(Texto(n, false));
                    Ocultar(e);
                }
                if (cota.Lineas.Count > 0 || cota.Textos.Count > 0)
                    movible.Cotas.Add(cota);
            }
        }
        if (movible.Ocultos.Count > 0)
            vista.HideElements(movible.Ocultos);

        double izq = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), der = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
        // Dentro de la viga: el recorte va justo a su alto y se come una cruz puesta más afuera (medido el 31-oct).
        double adentro = 0.02 * M;
        var marcaA = new PuntoModelo(izq, min.Z + adentro);
        var marcaB = new PuntoModelo(der, max.Z - adentro);
        Marcar(doc, vista, enPlano, derecha, marcaA);
        Marcar(doc, vista, enPlano, derecha, marcaB);
        doc.Regenerate();
        movible.Marcas = (marcaA, marcaB);
    }

    /// <summary>Una cruz de calibración en magenta, que <see cref="ExportadorImagen.Calibrar"/> encuentra en la imagen.</summary>
    internal static void Marcar(Document doc, View vista, XYZ enPlano, XYZ derecha, PuntoModelo p)
    {
        const double Largo = 0.015 * M;
        var magenta = new OverrideGraphicSettings();
        magenta.SetProjectionLineColor(new Color(255, 0, 255));
        magenta.SetProjectionLineWeight(1);
        XYZ En(double x, double z) => new XYZ(enPlano.X, enPlano.Y, z) + derecha * (x - enPlano.DotProduct(derecha));
        foreach (Line l in new[] { Line.CreateBound(En(p.X - Largo, p.Y), En(p.X + Largo, p.Y)), Line.CreateBound(En(p.X, p.Y - Largo), En(p.X, p.Y + Largo)) })
            vista.SetElementOverrides(doc.Create.NewDetailCurve(vista, l).Id, magenta);
    }

    /// <summary>
    /// Donde se apoya la viga: las columnas que tocan su caja (con 5 cm de holgura) y, en una vigueta, las vigas
    /// perpendiculares que la tocan y no son otra vigueta (decisión 3 del plan de viguetas). Se acotan y, desde #262, se
    /// muestran en la vista larga.
    /// </summary>
    private static List<Element> Apoyos(Document doc, VigaContinua viga, XYZ derecha, XYZ min, XYZ max)
    {
        double holgura = 0.05 * M;
        var caja = new Outline(new XYZ(min.X - holgura, min.Y - holgura, min.Z - holgura), new XYZ(max.X + holgura, max.Y + holgura, max.Z + holgura));
        var filtro = new BoundingBoxIntersectsFilter(caja);
        List<Element> apoyos = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType().WherePasses(filtro).ToList();
        if (viga.Elemento.EsVigueta)
            apoyos.AddRange(new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().WherePasses(filtro)
                .OfType<FamilyInstance>().Where(v => !viga.Vigas.Contains(v.Id) && v.Location is LocationCurve { Curve: Line l } && Math.Abs(l.Direction.DotProduct(derecha)) < 0.1
                    && !TipoElemento.EsVigueta(v.Symbol.Name, (int)Math.Round(LectorDespiece.Mm(Math.Abs((v.get_BoundingBox(null).Max - v.get_BoundingBox(null).Min).DotProduct(derecha)))))));
        return apoyos;
    }

    /// <summary>
    /// La cadena columna, luz libre, columna… en la línea de abajo y, arriba de todo, el largo completo de la viga (Juan,
    /// 31-oct; ubicación de #264). Las medidas se
    /// toman contra planos de referencia puestos en las caras de las columnas, solo en esta vista. Si Revit no acepta las
    /// cotas, se deshace lo hecho y la vista queda sin ellas: no vale tumbar la vista previa por esto.
    /// </summary>
    private static void CotarColumnas(Document doc, View vista, VigaContinua viga, XYZ centro, XYZ derecha, XYZ min, XYZ max, double fondo, double techo, TiposPropios tipos, CotasCreadas cotas)
    {
        double a = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), b = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
        List<Element> apoyos = Apoyos(doc, viga, derecha, min, max);
        List<(double Desde, double Hasta)> columnas = apoyos.Select(c => c.get_BoundingBox(null)).Where(c => c is not null)
            .Select(c => (Math.Min(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)), Math.Max(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)))).ToList();
        IReadOnlyList<TramoViga> tramos = TramosViga.Calcular(a, b, columnas);
        Registro.Paso($"cotas: {columnas.Count} columnas, {tramos.Count} tramos");

        XYZ En(double s, double z) => new XYZ(centro.X, centro.Y, z) + derecha * (s - centro.DotProduct(derecha));
        using var prueba = new SubTransaction(doc);
        prueba.Start();
        try
        {
            List<double> marcas = [tramos[0].Desde, .. tramos.Select(t => t.Hasta)];
            // Entran apenas en el recorte, que va justo al alto de la viga: un plano fuera de él deja su cota sin mostrar. En
            // blanco, porque ocultar su categoría ocultaría también las cotas (medido el 31-oct).
            var blanco = new OverrideGraphicSettings();
            blanco.SetProjectionLineColor(new Color(255, 255, 255));
            List<Reference> planos = marcas.Select(s =>
            {
                ReferencePlane plano = doc.Create.NewReferencePlane(En(s, min.Z + AlturaPlano), En(s, fondo - CotaLinea - AlturaPlano), vista.ViewDirection, vista);
                vista.SetElementOverrides(plano.Id, blanco);
                return plano.GetReference();
            }).ToList();
            doc.Regenerate();

            Dimension Cota(IEnumerable<Reference> referencias, double z)
            {
                var lista = new ReferenceArray();
                foreach (Reference r in referencias)
                    lista.Append(r);
                Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(En(marcas[0], z), En(marcas[^1], z)), lista);
                if (tipos.CotaCorta is { } tipo)
                    cota.DimensionType = tipo;
                return cota;
            }
            // Con un solo tramo la cadena y el total dirían lo mismo. La cadena va en la línea de abajo, junto con los ejes;
            // el largo, arriba de todo (#264). Las líneas de referencia cortas no cruzan las barras de arriba.
            Dimension? cadena = tramos.Count > 1 ? Cota(planos, fondo - CotaLinea) : null;
            Dimension total = Cota([planos[0], planos[^1]], techo + CotaArriba);
            prueba.Commit();
            if (cadena is not null)
                cotas.Abajo.Add(cadena);
            cotas.Arriba.Add(total);
        }
        catch (Exception e)
        {
            Registro.Paso("cotas de columnas: no se pudieron crear: " + e.Message);
            prueba.RollBack();
        }
    }

    /// <summary>
    /// Una etiqueta sobre la viga por cada conjunto de estribos, centrada en su zona (#224). Si no cabe junto a la anterior
    /// sube a otra fila, para que no se encimen. Devuelve cuántas filas usó. La grapa de una zona trae la misma cantidad y
    /// separación que su estribo y va 2 cm al lado (viga 388501): con la misma etiqueta en el mismo sitio, se escribe una vez.
    /// </summary>
    private static int EtiquetarEstribos(Document doc, View vista, List<Rebar> barras, XYZ derecha, XYZ enPlano, double techo, TiposPropios tipos, List<(Rebar Barra, TextNote Nota)> sueltas)
    {
        var finDeFila = new List<double>();
        var escritas = new List<(string Texto, double Centro)>();
        foreach ((Rebar b, double centro) in barras.Where(b => !EsLongitudinal(b, derecha))
            .Select(b => (b, CentroDelConjunto(b).DotProduct(derecha))).OrderBy(z => z.Item2))
        {
            string texto = LectorDespiece.EtiquetaEstribos(doc.GetElement(b.GetTypeId()).Name, b.Quantity, LectorDespiece.Separacion(b));
            if (escritas.Any(e => e.Texto == texto && Math.Abs(e.Centro - centro) < MismaZona))
                continue;
            escritas.Add((texto, centro));
            // Por lo que mide su texto, no el de una barra: con el de una barra se iban a una tercera fila (decisión 43).
            double medio = MedioAnchoTexto(texto);
            int fila = finDeFila.FindIndex(fin => centro - medio > fin + HuecoEnFila);
            if (fila < 0)
            {
                fila = finDeFila.Count;
                finDeFila.Add(0);
            }
            finDeFila[fila] = centro + medio;
            XYZ punto = enPlano + derecha * (centro - enPlano.DotProduct(derecha));
            TextNote nota = TextNote.Create(doc, vista.Id, new XYZ(punto.X, punto.Y, techo + AltoTexto + FilaEtiquetasEstribos * fila), texto, tipos.Nota(HorizontalTextAlignment.Center));
            Pintar(vista, nota.Id, b, null);
            sueltas.Add((b, nota));
        }
        return finDeFila.Count;
    }

    /// <summary>Las claves de los grupos de cotas que se arrastran (paso 10 del plan del 10-11).</summary>
    internal const string ClaveCotaAbajo = "cota-abajo", ClaveCotaArriba = "cota-arriba";

    /// <summary>Lo que el usuario corrió un grupo de cotas en el paso 2, en pies: a la derecha (columna) o hacia arriba (viga).</summary>
    internal static double Corrimiento(DespieceMovible movible, string clave, bool horizontal) =>
        movible.Movimientos.TryGetValue(clave, out Movimiento? m) ? (horizontal ? m.Dx : m.Dz) / 0.3048 : 0;

    /// <summary>Lo que se dibuja de cada línea de referencia en la página: los 2.4 mm de papel de <c>CotaCorta</c>.</summary>
    internal static double LineaReferencia => 0.0024 * EscalaLongitudinal * M;

    /// <summary>
    /// Mide una cota de Revit ya creada para que la página la dibuje: la línea, una línea de referencia y una rayita en cada
    /// marca, y cada número donde lo puso Revit. <paramref name="eje"/> es hacia dónde mide; <paramref name="referencia"/>,
    /// la línea de referencia desde la línea de cota (hacia lo acotado). El punto del número de una cota es la base del
    /// texto (medido el 05-nov): se pasa al centro, que es lo que dibuja la página.
    /// </summary>
    internal static void MedirCota(Dimension d, CotaMovible cota, XYZ derecha, XYZ eje, XYZ referencia, bool girada)
    {
        PuntoModelo P(XYZ p) => new(p.DotProduct(derecha), p.Z);
        double alto = Letra.Mm / 1000 * EscalaLongitudinal * M;
        var tramos = new List<(XYZ Origen, double Valor, string Texto, XYZ? Punto)>();
        if (d.NumberOfSegments == 0)
            tramos.Add((d.Origin, d.Value ?? 0, d.ValueString, d.TextPosition));
        else
            foreach (DimensionSegment s in d.Segments)
                tramos.Add((s.Origin, s.Value ?? 0, s.ValueString, s.TextPosition));
        var marcas = new List<XYZ>();
        foreach ((XYZ origen, double valor, string texto, XYZ? punto) in tramos)
        {
            marcas.Add(origen - eje * (valor / 2));
            marcas.Add(origen + eje * (valor / 2));
            if (punto is not null)
            {
                XYZ centroTexto = girada ? punto - derecha * (alto / 2) : punto + XYZ.BasisZ * (alto / 2);
                cota.Textos.Add(new TextoMovible(texto, P(centroTexto), girada));
            }
        }
        List<XYZ> distintas = marcas.GroupBy(m => Math.Round(m.DotProduct(eje) / 0.001)).Select(g => g.First()).OrderBy(m => m.DotProduct(eje)).ToList();
        if (distintas.Count < 2)
            return;
        cota.Lineas.Add((P(distintas[0]), P(distintas[^1])));
        foreach (XYZ m in distintas)
        {
            cota.Lineas.Add((P(m), P(m + referencia)));
            cota.Marcas.Add(P(m));
        }
    }

    /// <summary>Lleva los círculos de los ejes por encima de las filas de barras, para que no choquen, solo en esta vista.</summary>
    private static void AjustarEjes(Document doc, View vista, double arriba, double abajo)
    {
        foreach (Grid g in new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Grid)).Cast<Grid>())
        {
            Curve actual = g.GetCurvesInView(DatumExtentType.ViewSpecific, vista)[0];
            XYZ a = actual.GetEndPoint(0), e = actual.GetEndPoint(1);
            g.SetDatumExtentType(DatumEnds.End0, vista, DatumExtentType.ViewSpecific);
            g.SetDatumExtentType(DatumEnds.End1, vista, DatumExtentType.ViewSpecific);
            bool subeElPrimero = a.Z > e.Z;
            g.SetCurveInView(DatumExtentType.ViewSpecific, vista, Line.CreateBound(new XYZ(a.X, a.Y, subeElPrimero ? arriba : abajo), new XYZ(e.X, e.Y, subeElPrimero ? abajo : arriba)));
        }
    }

    /// <summary>
    /// Las luces entre ejes, en la línea de abajo con los números debajo, y el total en una nota (#264). Son cotas de Revit,
    /// ligadas a los ejes (#222). Con menos de dos ejes no hay qué acotar.
    /// </summary>
    private static void Cotar(Document doc, View vista, XYZ centro, XYZ derecha, double largo, XYZ min, XYZ max, double fondo, double techo, TiposPropios tipos, CotasCreadas cotas)
    {
        double desde = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), hasta = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
        List<Grid> ejes = new FilteredElementCollector(doc, vista.Id).OfClass(typeof(Grid)).Cast<Grid>()
            .Where(g => g.Curve is Line l && Math.Abs(l.Direction.DotProduct(derecha)) < 0.01)
            .Where(g => g.Curve.GetEndPoint(0).DotProduct(derecha) >= desde - 0.5 * M && g.Curve.GetEndPoint(0).DotProduct(derecha) <= hasta + 0.5 * M)
            .OrderBy(g => g.Curve.GetEndPoint(0).DotProduct(derecha)).ToList();
        if (ejes.Count < 2)
            return;

        Dimension Cota(IEnumerable<Grid> entre, double z)
        {
            var referencias = new ReferenceArray();
            foreach (Grid g in entre)
                referencias.Append(new Reference(g));
            XYZ medio = new(centro.X, centro.Y, z);
            Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(medio - derecha * (largo / 2), medio + derecha * (largo / 2)), referencias);
            if (tipos.Cota is { } tipo)
                cota.DimensionType = tipo;
            return cota;
        }

        // Una sola cadena en la línea de abajo, con los números debajo de ella: arriba van los de las caras (#264).
        double linea = fondo - CotaLinea;
        Dimension cadena = Cota(ejes, linea);
        doc.Regenerate();
        PasarTextoAbajo(cadena, linea);
        cotas.Abajo.Add(cadena);
        // El total entre ejes, en una nota al final de la cota de arriba, junto al largo de la viga (decisión 24 del plan de
        // #317; antes iba al final de la línea de abajo): así no se pierde ninguna medida.
        if (ejes.Count > 2)
        {
            double total = Math.Abs(ejes[^1].Curve.GetEndPoint(0).DotProduct(derecha) - ejes[0].Curve.GetEndPoint(0).DotProduct(derecha));
            XYZ punto = new XYZ(centro.X, centro.Y, techo + CotaArriba + AltoTexto) + derecha * (largo / 2 + 0.10 * M * Letra.Factor);
            cotas.Arriba.Add(TextNote.Create(doc, vista.Id, punto, "Ejes " + Cadena(total), tipos.Nota(HorizontalTextAlignment.Left)));
        }
    }

    /// <summary>
    /// Pasa los números de una cota debajo de su línea, como reflejados (#264). Revit los pone arriba; se mueve su punto de
    /// arrastre (<c>TextPosition</c>, API pública desde 2015) al otro lado, a la misma distancia más el alto de la letra.
    /// Si Revit no deja mover alguno, queda arriba. Se registran las posiciones para medirlas en Revit (plan de #266, paso 4).
    /// </summary>
    private static void PasarTextoAbajo(Dimension cota, double linea)
    {
        double alto = Letra.Mm / 1000 * EscalaLongitudinal * M;
        // El punto es la base del texto, 0.002 m sobre la línea (medido el 05-nov). Reflejado, la base baja 1.5 veces el alto:
        // medio alto de letra de aire entre la línea y el número (decisión 10 del plan de #266).
        XYZ Abajo(XYZ p) => new(p.X, p.Y, linea - (p.Z - linea) - 1.5 * alto);
        try
        {
            if (cota.NumberOfSegments == 0)
            {
                if (cota.IsTextPositionAdjustable() && cota.TextPosition is { } p)
                {
                    cota.TextPosition = Abajo(p);
                    Registro.Paso($"cotas: texto de ejes {(p.Z - linea) * 0.3048:0.000} m sobre la línea, pasado abajo");
                }
                return;
            }
            foreach (DimensionSegment segmento in cota.Segments)
                if (segmento.IsTextPositionAdjustable() && segmento.TextPosition is { } p)
                {
                    segmento.TextPosition = Abajo(p);
                    Registro.Paso($"cotas: texto de ejes {(p.Z - linea) * 0.3048:0.000} m sobre la línea, pasado abajo");
                }
        }
        catch (Exception e)
        {
            Registro.Paso("cotas: no se pudo pasar el texto de ejes abajo: " + e.Message);
        }
    }

    /// <summary>
    /// Pone cada barra en la primera fila donde quepa, de la viga hacia afuera, con su etiqueta encima. Devuelve hasta qué
    /// altura llegó la última fila.
    /// </summary>
    private static void Acomodar(Document doc, View vista, List<Sacada> sacadas, Pila pila, TiposPropios tipos)
    {
        // El reparto vive en AcomodadorFilas, con sus pruebas de que nada se encima. Aquí solo importa a qué fila va cada una.
        IEnumerable<PiezaFila> piezas = sacadas.Select((s, i) =>
        {
            double medio = (s.Izquierda + s.Derecha) / 2;
            return new PiezaFila(i, Math.Min(s.Izquierda, medio - MedioAnchoEtiqueta), Math.Max(s.Derecha, medio + MedioAnchoEtiqueta), 0);
        });
        List<List<(Sacada, double)>> filas = AcomodadorFilas.Acomodar(piezas, 0, true, HuecoEnFila, 0).Piezas.GroupBy(u => u.Fila).OrderBy(g => g.Key)
            .Select(g => g.Select(u => (sacadas[u.Pieza.Indice], 0.0)).ToList()).ToList();
        ColocarFilas(doc, vista, filas, pila, tipos);
    }

    /// <summary>
    /// Todas las piezas de una misma línea de acero (S1, S2… arriba; I1, I2… abajo) en una sola fila, como en el plano de
    /// Santiago: las que se traslapan quedan en la misma fila y una se corre <see cref="DesfaseTraslapo"/> para que se vea
    /// el traslapo (#223). Los bastones van entre esta fila y la viga.
    /// </summary>
    private static void AcomodarLinea(Document doc, View vista, List<Sacada> sacadas, Pila pila, TiposPropios tipos)
    {
        if (sacadas.Count == 0)
            return;
        XYZ derecha = pila.Derecha;
        var fila = new List<(Sacada, double)>();
        var traslapos = new List<Traslapo>();
        (double Desde, double Hasta)? anterior = null;
        double hasta = double.MinValue;
        int escalon = 0;
        foreach (Sacada s in sacadas.OrderBy(s => s.Izquierda))
        {
            escalon = s.Izquierda < hasta ? escalon + 1 : 0;
            hasta = Math.Max(hasta, s.Derecha);
            fila.Add((s, escalon * DesfaseTraslapo));

            // El traslapo sale de los tramos rectos reales de las dos piezas, no de lo que ocupan sus detalles.
            Curve mayor = Mayor(s.Barra);
            double a = mayor.GetEndPoint(0).DotProduct(derecha), e = mayor.GetEndPoint(1).DotProduct(derecha);
            (double Desde, double Hasta) tramo = (Math.Min(a, e), Math.Max(a, e));
            if (anterior is { } previo && Math.Min(previo.Hasta, tramo.Hasta) - Math.Max(previo.Desde, tramo.Desde) > MinimoTraslapo)
            {
                double inicio = Math.Max(previo.Desde, tramo.Desde), fin = Math.Min(previo.Hasta, tramo.Hasta);
                traslapos.Add(new Traslapo(s, (inicio + fin) / 2, fin - inicio));
            }
            anterior = tramo;
        }
        ColocarFilas(doc, vista, [fila], pila, tipos, traslapos);
    }

    /// <summary>
    /// Un lado de la viga (arriba o abajo): todo lo que ya ocupa lugar ahí, medido hacia afuera desde la cara de la viga
    /// (<see cref="Afuera"/>), para que el <see cref="Empacador"/> ponga cada fila lo más cerca sin tocar nada (paso 7 del
    /// plan del 10-11). Empieza con los obstáculos: el asomo de las columnas y las etiquetas de los estribos.
    /// </summary>
    private sealed class Pila(double cara, bool haciaArriba, XYZ derecha)
    {
        public bool HaciaArriba { get; } = haciaArriba;

        public XYZ Derecha { get; } = derecha;

        public List<Rectangulo> Puestos { get; } = [];

        /// <summary>La línea de la última fila puesta, hacia afuera; las filas van en orden.</summary>
        public double? Linea { get; set; }

        /// <summary>Hasta dónde llega lo más lejano puesto, hacia afuera.</summary>
        public double Hasta { get; set; }

        public double Afuera(double z) => HaciaArriba ? z - cara : cara - z;

        public double Z(double afuera) => HaciaArriba ? cara + afuera : cara - afuera;

        /// <summary>Un obstáculo medido en la vista: lo que ocupa una caja, si cae de este lado de la viga.</summary>
        public void Obstaculo(double desde, double hasta, double zMin, double zMax)
        {
            (double a, double b) = (Afuera(zMin), Afuera(zMax));
            if (Math.Max(a, b) <= 0)
                return;
            Puestos.Add(new Rectangulo(desde, hasta, Math.Max(0, Math.Min(a, b)), Math.Max(a, b)));
            Hasta = Math.Max(Hasta, Math.Max(a, b));
        }

        /// <summary>
        /// Lo que ocupa algo que está entre <paramref name="dzMin"/> y <paramref name="dzMax"/> respecto de la línea de su
        /// fila (en z), pasado a "hacia afuera".
        /// </summary>
        public Rectangulo Parte(double desde, double hasta, double dzMin, double dzMax) =>
            HaciaArriba ? new Rectangulo(desde, hasta, dzMin, dzMax) : new Rectangulo(desde, hasta, -dzMax, -dzMin);
    }

    /// <summary>Un traslapo de una fila: su centro a lo largo de la vista y su largo, en pies.</summary>
    private sealed record Traslapo(Sacada Pieza, double Centro, double Largo);

    /// <summary>
    /// Coloca las filas de un lado de la viga, en orden, cada una lo más cerca de la viga sin tocar nada de lo ya puesto
    /// (<see cref="Empacador"/>, paso 7 del plan del 10-11). Antes cada fila se apoyaba en la anterior con lo que más
    /// sobresalía de las dos (decisión 39 del plan de #317), sin mirar dónde: un gancho podía cruzar el texto de la otra
    /// fila (captura 15 de Santiago). Ahora cada fila se describe por partes (cada tramo de la barra, su etiqueta, la
    /// medida de cada gancho y de cada traslapo) y solo choca lo que está a la misma altura a lo largo de la viga. Cada
    /// pieza puede ir corrida un desfase; la etiqueta va sobre su tramo recto.
    /// </summary>
    private static void ColocarFilas(Document doc, View vista, List<List<(Sacada S, double Desfase)>> filas, Pila pila, TiposPropios tipos, List<Traslapo>? traslapos = null)
    {
        XYZ derecha = pila.Derecha;
        foreach (List<(Sacada S, double Desfase)> fila in filas)
        {
            // Dónde va la medida de cada traslapo a lo largo: no depende de la altura de la fila.
            var medidas = new List<(Traslapo T, double Centro, double Sobre)>();
            foreach (Traslapo t in traslapos ?? [])
            {
                // Justo sobre la más alta de las dos barras que se traslapan, no a la altura de la fila (Juan, 31-oct). Si ahí mismo
                // hay una etiqueta, se corre a su costado del lado del traslapo; antes subía una fila y quedaba lejos de su barra
                // (Luciano, 09-nov, decisión 41 del plan de #317).
                double sobreTraslapo = fila.First(f => ReferenceEquals(f.S, t.Pieza)).Desfase;
                double centro = t.Centro;
                // Con el ancho real de cada etiqueta y de la medida (por su texto): con un ancho fijo, la medida quedaba encima
                // de una etiqueta larga (`3 #7 L= 2.60/3.001.34`, captura de V C del 10-11). Si al correrse cae sobre otra
                // etiqueta, se vuelve a correr.
                double medioMedida = MedioAnchoTexto((t.Largo * 0.3048).ToString("0.00", CultureInfo.InvariantCulture));
                var etiquetas = fila.Where(f => Math.Abs(f.Desfase - sobreTraslapo) < AltoTexto)
                    .Select(f => (Centro: f.S.Posicion.DotProduct(derecha), Medio: MedioAnchoTexto(TextoEtiqueta(doc, f.S)))).ToList();
                for (int vuelta = 0; vuelta < etiquetas.Count; vuelta++)
                {
                    double c = centro;
                    var choca = etiquetas.Where(e => Math.Abs(e.Centro - c) < e.Medio + medioMedida).OrderBy(e => Math.Abs(e.Centro - t.Centro)).ToList();
                    if (choca.Count == 0)
                        break;
                    var e = choca[0];
                    centro = e.Centro + (t.Centro >= e.Centro ? 1 : -1) * (e.Medio + medioMedida + AireNota);
                }
                medidas.Add((t, centro, sobreTraslapo));
            }

            List<Rectangulo> partes = [];
            foreach ((Sacada s, double desfase) in fila)
                partes.AddRange(Partes(doc, s, desfase, pila));
            foreach ((Traslapo t, double centro, double sobre) in medidas)
            {
                double medio = MedioAnchoTexto((t.Largo * 0.3048).ToString("0.00", CultureInfo.InvariantCulture));
                partes.Add(pila.Parte(centro - medio, centro + medio, sobre, sobre + AltoTexto));
            }
            double minimo = pila.Linea is { } anterior ? anterior + HuecoFilas : double.NegativeInfinity;
            double afuera = Empacador.Colocar([new GrupoEmpacado(partes, minimo)], 0, HuecoFilas, HuecoFilas, obstaculos: pila.Puestos)[0];
            pila.Puestos.AddRange(partes.Select(p => p with { Cerca = p.Cerca + afuera, Lejos = p.Lejos + afuera }));
            pila.Linea = afuera;
            pila.Hasta = Math.Max(pila.Hasta, partes.Max(p => p.Lejos) + afuera);
            double linea = pila.Z(afuera);
            Registro.Paso($"longitudinal: fila de {fila.Count} a {afuera * 0.3048:0.000} de la viga ({(pila.HaciaArriba ? "arriba" : "abajo")}), {partes.Count} partes");

            foreach ((Sacada s, double desfase) in fila)
            {
                s.Final = new XYZ(s.Posicion.X, s.Posicion.Y, linea - s.Linea - s.Abajo + desfase);
                RebarBendingDetail.SetPosition(s.Detalle, s.Final);
                TextNote nota = TextNote.Create(doc, vista.Id, new XYZ(s.Posicion.X, s.Posicion.Y, linea + s.CotaEncima + desfase + AltoTexto), TextoEtiqueta(doc, s), tipos.Nota(HorizontalTextAlignment.Center));
                Pintar(vista, nota.Id, s.Barra, null);
                s.Etiqueta = nota;
                MedirGanchos(doc, vista, s, linea + desfase, tipos);
            }
            // La medida de cada traslapo, sobre él (#223). Es texto: un detalle de plegado no da referencias para una cota
            // de Revit (probado el 30-oct).
            foreach ((Traslapo t, double centro, double sobreTraslapo) in medidas)
            {
                XYZ punto = t.Pieza.Posicion + derecha * (centro - t.Pieza.Posicion.DotProduct(derecha));
                double alto = linea + sobreTraslapo + AltoTexto;
                TextNote nota = TextNote.Create(doc, vista.Id, new XYZ(punto.X, punto.Y, alto), (t.Largo * 0.3048).ToString("0.00", CultureInfo.InvariantCulture), tipos.Nota(HorizontalTextAlignment.Center));
                Pintar(vista, nota.Id, t.Pieza.Barra, null);
                t.Pieza.Anexos.Add(nota);
            }
        }
    }

    /// <summary>La etiqueta de una barra sacada: <c>3 #7 L= 2.60/3.00</c>.</summary>
    private static string TextoEtiqueta(Document doc, Sacada s) =>
        LectorDespiece.Etiqueta(doc.GetElement(s.Barra.GetTypeId()).Name, s.Cantidad, LectorDespiece.LargoBarra(s.Barra), Curvas(s.Barra, true).Sum(c => c.Length));

    /// <summary>
    /// Lo que ocupa una barra sacada con sus textos, por partes y respecto de la línea de su fila: cada tramo de la barra
    /// (como se dibuja, con un poco de grueso), la etiqueta sobre el tramo recto y la medida girada de cada gancho, como las
    /// pone <see cref="MedirGanchos"/>.
    /// </summary>
    private static List<Rectangulo> Partes(Document doc, Sacada s, double desfase, Pila pila)
    {
        XYZ derecha = pila.Derecha;
        var partes = new List<Rectangulo>();
        IList<Curve> curvas = Curvas(s.Barra, false);
        double zRecto = Mayor(s.Barra).GetEndPoint(0).Z;
        List<XYZ> puntos = curvas.SelectMany(c => c.Tessellate()).ToList();
        double dx = s.Izquierda - puntos.Min(p => p.DotProduct(derecha));
        double grueso = AireNota;
        foreach (Curve c in curvas)
        {
            IList<XYZ> t = c.Tessellate();
            for (int i = 0; i + 1 < t.Count; i++)
            {
                double x0 = t[i].DotProduct(derecha) + dx, x1 = t[i + 1].DotProduct(derecha) + dx;
                double z0 = t[i].Z - zRecto + desfase, z1 = t[i + 1].Z - zRecto + desfase;
                partes.Add(pila.Parte(Math.Min(x0, x1) - grueso, Math.Max(x0, x1) + grueso, Math.Min(z0, z1) - grueso, Math.Max(z0, z1) + grueso));
            }
        }
        string texto = TextoEtiqueta(doc, s);
        double centro = s.Posicion.DotProduct(derecha), medio = MedioAnchoTexto(texto);
        partes.Add(pila.Parte(centro - medio, centro + medio, s.CotaEncima + desfase, s.CotaEncima + desfase + AltoTexto));

        if (curvas.Count >= 3)
        {
            double d = ((RebarBarType)doc.GetElement(s.Barra.GetTypeId())).BarNominalDiameter;
            Curve mayor = Mayor(s.Barra);
            double medioBarra = (mayor.GetEndPoint(0) + mayor.GetEndPoint(1)).DotProduct(derecha) / 2;
            double medioNumero = MedioAnchoTexto("0.00");
            void Medida(Curve pata, Curve doblez)
            {
                double largo = pata.Length + (doblez is Arc arco ? arco.Radius : 0) + d / 2;
                XYZ centroPata = (pata.GetEndPoint(0) + pata.GetEndPoint(1)) / 2;
                bool aLaDerecha = centroPata.DotProduct(derecha) > medioBarra;
                double x = aLaDerecha ? s.Derecha + AireNota : s.Izquierda - AnchoNotaGirada;
                double z = desfase + (centroPata.Z > zRecto ? largo : -largo) / 2;
                partes.Add(pila.Parte(x, x + AnchoNotaGirada, z - medioNumero, z + medioNumero));
            }
            if (s.Barra.GetHookTypeId(0) != ElementId.InvalidElementId)
                Medida(curvas[0], curvas[1]);
            if (s.Barra.GetHookTypeId(1) != ElementId.InvalidElementId)
                Medida(curvas[^1], curvas[^2]);
        }
        return partes;
    }

    /// <summary>
    /// La medida de cada gancho, girada y por fuera de su pata. Es texto porque Revit no deja mostrar la del gancho sin la
    /// del tramo recto, y esa ya va en la etiqueta (decisión 9 del plan del 31-oct).
    /// </summary>
    private static void MedirGanchos(Document doc, View vista, Sacada s, double linea, TiposPropios tipos)
    {
        IList<Curve> curvas = Curvas(s.Barra, false);
        if (curvas.Count < 3)
            return;
        Curve mayor = Mayor(s.Barra);
        XYZ derecha = vista.RightDirection;
        double medio = (mayor.GetEndPoint(0) + mayor.GetEndPoint(1)).DotProduct(derecha) / 2, alturaTramo = mayor.GetEndPoint(0).Z;
        double d = ((RebarBarType)doc.GetElement(s.Barra.GetTypeId())).BarNominalDiameter;

        void Medir(Curve pata, Curve doblez)
        {
            double radio = doblez is Arc arco ? arco.Radius : 0;
            double largo = pata.Length + radio + d / 2;
            XYZ centroPata = (pata.GetEndPoint(0) + pata.GetEndPoint(1)) / 2;
            double x = centroPata.DotProduct(derecha) > medio ? s.Derecha + AireNota : s.Izquierda - AnchoNotaGirada;
            double z = linea + (centroPata.Z > alturaTramo ? largo : -largo) / 2;
            TextNoteOptions girada = tipos.Nota(HorizontalTextAlignment.Center);
            girada.Rotation = Math.PI / 2;
            XYZ punto = new XYZ(s.Posicion.X, s.Posicion.Y, z) + derecha * (x - s.Posicion.DotProduct(derecha));
            TextNote nota = TextNote.Create(doc, vista.Id, punto, (largo * 0.3048).ToString("0.00", CultureInfo.InvariantCulture), girada);
            Pintar(vista, nota.Id, s.Barra, null);
            s.Anexos.Add(nota);
        }

        if (s.Barra.GetHookTypeId(0) != ElementId.InvalidElementId)
            Medir(curvas[0], curvas[1]);
        if (s.Barra.GetHookTypeId(1) != ElementId.InvalidElementId)
            Medir(curvas[^1], curvas[^2]);
    }

    private static View Seccion(Document doc, ViewFamilyType tipo, VigaContinua viga, List<Rebar> barras, TiposPropios tipos)
    {
        Letra.EnSeccion();
        (XYZ min, XYZ max) = Caja(doc, viga);
        XYZ dir = viga.Direccion;
        XYZ base_ = dir.CrossProduct(XYZ.BasisZ).Negate();
        double ancho = Math.Abs((max - min).DotProduct(base_));
        double alto = max.Z - min.Z;
        XYZ medio = (min + max) / 2;
        double aLoLargo = PuntoDeCorte(doc, viga, barras, dir, min, max);
        XYZ corte = medio + dir * (aLoLargo - medio.DotProduct(dir));

        Transform t = Transform.Identity;
        t.BasisX = base_;
        t.BasisY = XYZ.BasisZ;
        t.BasisZ = base_.CrossProduct(XYZ.BasisZ);
        t.Origin = corte;
        var caja = new BoundingBoxXYZ { Transform = t, Min = new XYZ(-1, -1, -FondoSeccion), Max = new XYZ(1, 1, 0) };
        ViewSection vista = Preparar(doc, tipo, caja, "S", EscalaSeccion);
        // Revit puede voltear la vista: lo que manda es su derecha real.
        XYZ derecha = vista.RightDirection;
        // Justa: la sección a la izquierda y el estribo a su lado (captura de Santiago).
        Recortar(vista, corte, ancho / 2 + 0.55 * M * Letra.Factor, ancho / 2 + 1.05 * M * Letra.Factor, alto / 2 + 0.7 * M * Letra.Factor, alto / 2 + 0.7 * M * Letra.Factor);
        Limpiar(doc, vista, viga, barras);
        Colorear(doc, vista, barras);
        vista.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true);
        vista.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true);
        var gris = new OverrideGraphicSettings();
        gris.SetCutBackgroundPatternId(Solido(doc));
        gris.SetCutBackgroundPatternColor(new Color(205, 205, 205));
        // Solo lo cortado: lo que la vista ve de la viga detrás del corte (más ancha, o metida en una zapata) salía como
        // una franja negra o gris oscuro, y Santiago no la quiere (#314, decisión 37 del plan de #317).
        gris.SetProjectionLineColor(new Color(255, 255, 255));
        gris.SetSurfaceForegroundPatternVisible(false);
        gris.SetSurfaceBackgroundPatternVisible(false);
        gris.SetSurfaceTransparency(100);
        foreach (ElementId id in viga.Vigas)
            vista.SetElementOverrides(id, gris);
        (double Izq, double Der, double Abajo, double Arriba)? completa = SeccionCompleta(doc, vista, viga, corte, dir, derecha);
        vista.DisplayStyle = DisplayStyle.FlatColors;
        doc.Regenerate();

        // La vista tiene fondo para alcanzar un estribo: se veían barras que terminan detrás del corte, sin etiqueta y como si
        // fueran del corte (viga 388501: 10 puntos #4 y 5 etiquetas). Solo se deja lo que el corte atraviesa.
        List<ElementId> detras = barras.Where(b => EsLongitudinal(b, dir)).Where(b =>
        {
            Curve mayor = Mayor(b);
            double pa = mayor.GetEndPoint(0).DotProduct(dir), pe = mayor.GetEndPoint(1).DotProduct(dir);
            return aLoLargo < Math.Min(pa, pe) || aLoLargo > Math.Max(pa, pe);
        }).Select(b => b.Id).Where(id => doc.GetElement(id).CanBeHidden(vista)).ToList();
        if (detras.Count > 0)
            vista.HideElements(detras);

        // Las barras que pasan por el corte, para la leyenda: sin etiquetas con flechas ni cantidades, solo el color y el
        // diámetro (#313, Santiago 09-nov: "quitar la cantidad de varillas y solo poner una leyenda").
        var cortadas = new List<Rebar>();
        foreach (Rebar b in barras.Where(b => EsLongitudinal(b, dir)))
        {
            for (int i = 0; i < b.Quantity; i++)
            {
                // Con la curva transformada: el add-in de vigas corre barras dentro del conjunto (MoveBarInSet).
                Curve mayor = b.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, i).OrderByDescending(c => c.Length).First();
                double pa = mayor.GetEndPoint(0).DotProduct(dir), pe = mayor.GetEndPoint(1).DotProduct(dir);
                if (aLoLargo >= Math.Min(pa, pe) && aLoLargo <= Math.Max(pa, pe))
                    cortadas.Add(b);
            }
        }
        // Como la de la columna (decisión 22 del plan de #287): el cuadrito en el color y el texto en negro, uno debajo de
        // otro, el más grueso primero. Un renglón por diámetro, sin `piel` (punto 14 de Santiago, decisión 2 del plan del
        // 10-11; antes la piel iba en su fila, decisión 5 del plan de #317).
        var leyenda = cortadas.GroupBy(b => (Numero: LectorDespiece.Numero(doc.GetElement(b.GetTypeId()).Name), Nombre: NombreBarra(doc, b)))
            .OrderByDescending(g => g.Key.Numero).Select(g => g.Key).ToList();
        // Los estribos y menores, en el color de su diámetro, si ese diámetro no está ya (decisión 15 del plan de #317).
        foreach (Rebar b in barras.Where(b => !EsLongitudinal(b, dir)).GroupBy(b => NombreBarra(doc, b)).Select(g => g.First()))
        {
            string nombre = NombreBarra(doc, b);
            if (!leyenda.Any(l => l.Nombre == nombre))
                leyenda.Add((LectorDespiece.Numero(doc.GetElement(b.GetTypeId()).Name), nombre));
        }
        XYZ esquinaIzquierda = corte - derecha * (ancho / 2);
        for (int i = 0; i < leyenda.Count; i++)
        {
            XYZ fila = esquinaIzquierda - XYZ.BasisZ * (alto / 2 + LeyendaSeccion + i * FilaLeyenda);
            TextNote cuadro = TextNote.Create(doc, vista.Id, fila, "■", tipos.Nota(HorizontalTextAlignment.Left));
            (byte r, byte v, byte a) = ColoresDiametro.Rgb(leyenda[i].Numero);
            Columnas.SeccionColumna.Pintar(vista, cuadro.Id, new Color(r, v, a), siempre: true);
            TextNote.Create(doc, vista.Id, fila + derecha * AnchoCuadro, leyenda[i].Nombre, tipos.Nota(HorizontalTextAlignment.Left));
        }
        Registro.Paso("sección: leyenda " + string.Join(", ", leyenda.Select(l => l.Nombre)));

        // Dónde se cortó, desde el extremo izquierdo de la viga como se ve en la vista larga (#314, decisión 6 del plan de #317).
        // Desde el comienzo del dibujo de la vista larga, donde arranca la cota del largo total (la cara de afuera de la
        // primera columna): así x se lee sobre esa cota, de izquierda a derecha (decisión 38 del plan de #317).
        XYZ lectura = SentidoDeLectura(doc, viga);
        double inicio = InicioDelDibujo(doc, viga, lectura, min, max);
        double x = corte.DotProduct(lectura) - inicio;
        TextNote.Create(doc, vista.Id, corte + XYZ.BasisZ * (alto / 2 + SobreSeccion), TextosDespiece.Corte(x * 0.3048), tipos.Nota(HorizontalTextAlignment.Center));
        Registro.Paso($"sección: corte en x= {x * 0.3048:0.000} desde el extremo izquierdo");

        double izquierda = ancho / 2 + TotalSeccion + 0.15 * M * Letra.Factor;
        double arriba = alto / 2 + SobreSeccion + 0.10 * M * Letra.Factor;
        double abajo = alto / 2 + LeyendaSeccion + Math.Max(0, leyenda.Count - 1) * FilaLeyenda + 0.15 * M * Letra.Factor;
        // El estribo aparte, cerrado como lo dibuja Revit (#313). El cerrado es el de más curvas: la grapa de la misma zona
        // también cruza el corte.
        Rebar? estribo = new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Rebar).OfType<Rebar>()
            .Where(b => viga.Vigas.Contains(b.GetHostId()) && !EsLongitudinal(b, dir))
            .OrderByDescending(b => Curvas(b, false).Count).FirstOrDefault();
        if (estribo is not null)
        {
            XYZ centroDetalle = corte + derecha * 0.7 * M * Letra.Factor;
            List<List<Rebar>> formas = FormasEstribos(barras.Where(b => !EsLongitudinal(b, dir)).ToList(), estribo);
            (double techo, double piso) = ApilarEstribos(doc, vista, formas, estribo, centroDetalle, tipos);
            arriba = Math.Max(arriba, techo - medio.Z + 0.05 * M * Letra.Factor);
            abajo = Math.Max(abajo, medio.Z - piso + 0.10 * M * Letra.Factor);
        }
        Recortar(vista, corte, izquierda, ancho / 2 + 1.05 * M * Letra.Factor, abajo, arriba);
        Acotar(doc, vista, viga, estribo, corte, derecha, medio.Z, tipos, completa);
        return vista;
    }

    /// <summary>
    /// La sección con la viga entera aunque una losa o una columna le coma un pedazo (punto 15 de Santiago, decisión 3 del
    /// plan del 10-11): como en la vista larga (<see cref="ContornoCompleto"/>), la viga de Revit no se dibuja y va su
    /// contorno en líneas negras. El contorno sale de la geometría de la viga antes de uniones y cortes
    /// (<c>GetOriginalGeometry</c>, en coordenadas de la familia: se lleva al modelo con <c>GetTotalTransform</c>, como en
    /// cuadro-cantidades). Si no se puede, queda el corte de Revit como antes.
    /// </summary>
    private static (double Izq, double Der, double Abajo, double Arriba)? SeccionCompleta(Document doc, View vista, VigaContinua viga, XYZ corte, XYZ dir, XYZ derecha)
    {
        try
        {
            // La pieza de la viga continua donde cae el corte.
            FamilyInstance? pieza = viga.Vigas.Select(id => doc.GetElement(id)).OfType<FamilyInstance>().FirstOrDefault(e =>
            {
                BoundingBoxXYZ? c = e.get_BoundingBox(null);
                if (c is null)
                    return false;
                double a = c.Min.DotProduct(dir), b = c.Max.DotProduct(dir), x = corte.DotProduct(dir);
                return x >= Math.Min(a, b) - 0.01 * M && x <= Math.Max(a, b) + 0.01 * M;
            });
            if (pieza is null)
            {
                Registro.Paso("sección: sin contorno completo, ninguna pieza en el corte");
                return null;
            }
            Transform total = pieza.GetTotalTransform();
            List<XYZ> puntos = [];
            foreach (Solid s in (pieza.GetOriginalGeometry(new Options()) ?? Enumerable.Empty<GeometryObject>()).OfType<Solid>().Where(s => s.Volume > 0))
                foreach (Edge e in s.Edges)
                    puntos.AddRange(e.Tessellate().Select(total.OfPoint));
            if (puntos.Count == 0)
            {
                Registro.Paso("sección: sin contorno completo, la viga no da geometría original");
                return null;
            }
            // Viga prismática: lo que ocupa a lo ancho y en alto es su sección, se corte donde se corte.
            double izq = puntos.Min(p => p.DotProduct(derecha)), der = puntos.Max(p => p.DotProduct(derecha));
            double abajo = puntos.Min(p => p.Z), arriba = puntos.Max(p => p.Z);
            BoundingBoxXYZ real = pieza.get_BoundingBox(null);
            double realIzq = Math.Min(real.Min.DotProduct(derecha), real.Max.DotProduct(derecha)), realDer = Math.Max(real.Min.DotProduct(derecha), real.Max.DotProduct(derecha));
            Registro.Paso($"sección: contorno completo {(der - izq) * 0.3048:0.000} × {(arriba - abajo) * 0.3048:0.000} (z {abajo * 0.3048:0.000} a {arriba * 0.3048:0.000});"
                + $" la caja de Revit {(realDer - realIzq) * 0.3048:0.000} × {(real.Max.Z - real.Min.Z) * 0.3048:0.000} (z {real.Min.Z * 0.3048:0.000} a {real.Max.Z * 0.3048:0.000})");
            // Si no cae donde está la viga, la transformación no era la que se creía: mejor el corte de Revit que un contorno corrido.
            if (der < realIzq + 0.01 * M || izq > realDer - 0.01 * M || arriba < real.Min.Z + 0.01 * M || abajo > real.Max.Z - 0.01 * M)
            {
                Registro.Paso("sección: el contorno completo no cae sobre la viga; queda el corte de Revit");
                return null;
            }

            var invisible = new OverrideGraphicSettings();
            invisible.SetProjectionLineColor(new Color(255, 255, 255));
            invisible.SetCutLineColor(new Color(255, 255, 255));
            invisible.SetSurfaceForegroundPatternVisible(false);
            invisible.SetSurfaceBackgroundPatternVisible(false);
            invisible.SetCutForegroundPatternVisible(false);
            invisible.SetCutBackgroundPatternVisible(false);
            invisible.SetSurfaceTransparency(100);
            foreach (ElementId id in viga.Vigas)
                vista.SetElementOverrides(id, invisible);
            XYZ En(double x, double z) => new XYZ(corte.X, corte.Y, z) + derecha * (x - corte.DotProduct(derecha));
            var negro = new OverrideGraphicSettings();
            negro.SetProjectionLineColor(new Color(0, 0, 0));
            negro.SetProjectionLineWeight(3);
            XYZ[] esquinas = [En(izq, abajo), En(der, abajo), En(der, arriba), En(izq, arriba)];
            for (int i = 0; i < 4; i++)
                vista.SetElementOverrides(doc.Create.NewDetailCurve(vista, Line.CreateBound(esquinas[i], esquinas[(i + 1) % 4])).Id, negro);
            return (izq, der, abajo, arriba);
        }
        catch (Exception e)
        {
            Registro.Paso("sección: contorno completo no se pudo: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Cada forma de estribo aparte, la principal primero y las menores (grapas, cajas) apiladas debajo, en la sección de la
    /// viga y en la de la columna. Cada una cerrada como la dibuja Revit y en el color de su diámetro, con su encabezado
    /// encima (<c>Cantidad: 84</c>, <c>L= 1.24 m</c>, <c>G= 0.08</c>), su ancho y su alto acotados una vez y su diámetro
    /// debajo (<c>#3</c>); los textos y las cotas en negro (#313, decisiones 14 y 18 del plan de #317). "Arriba" es el de
    /// la vista: Z en la sección de la viga, Y en la planta de la columna. Revit solo dibuja el detalle de una barra que se
    /// ve en la vista (31-oct: lanza ArgumentException); de una forma que no se ve van solo sus textos. Devuelve hasta
    /// dónde subió y bajó, medido sobre "arriba".
    /// </summary>
    internal static (double Techo, double Piso) ApilarEstribos(Document doc, View vista, List<List<Rebar>> formas, Rebar principal, XYZ punto, TiposPropios tipos)
    {
        XYZ arriba = vista.UpDirection, derecha = vista.RightDirection;
        double Alto(XYZ p) => p.DotProduct(arriba);
        XYZ En(double alto) => punto + arriba * (alto - Alto(punto));
        var enLaVista = new HashSet<ElementId>(new FilteredElementCollector(doc, vista.Id).OfCategory(BuiltInCategory.OST_Rebar).ToElementIds());
        double? tope = null;
        double techo = Alto(punto), piso = Alto(punto);
        foreach (List<Rebar> forma in formas)
        {
            // La forma de más piezas va primero en la lista (FormasEstribos): se dibuja esa si se ve.
            Rebar b = forma.Contains(principal) ? principal : forma.FirstOrDefault(x => enLaVista.Contains(x.Id)) ?? forma[0];
            // Lo alto del detalle; la primera forma queda centrada en el punto y las demás debajo de la anterior.
            double arribaDetalle = tope is { } t ? t - AireNota - AltoEncabezado : Alto(punto), abajoDetalle = arribaDetalle;
            if (enLaVista.Contains(b.Id))
            {
                try
                {
                    Element d = RebarBendingDetail.Create(doc, vista.Id, b.Id, 0, tipos.Barra, punto, 0);
                    Pintar(vista, d.Id, b, 3);
                    doc.Regenerate();
                    BoundingBoxXYZ m = d.get_BoundingBox(vista);
                    double sube = Math.Max(Alto(m.Min), Alto(m.Max)), baja = Math.Min(Alto(m.Min), Alto(m.Max));
                    double corrimiento = tope is null ? 0 : arribaDetalle - sube;
                    if (corrimiento != 0)
                        RebarBendingDetail.SetPosition(d, punto + arriba * corrimiento);
                    arribaDetalle = sube + corrimiento;
                    abajoDetalle = baja + corrimiento;
                    double centro = (m.Min.DotProduct(derecha) + m.Max.DotProduct(derecha)) / 2;
                    abajoDetalle = AcotarEstribo(doc, vista, b, punto + derecha * (centro - punto.DotProduct(derecha)), arribaDetalle, abajoDetalle, tipos);
                }
                catch (Exception e)
                {
                    Registro.Paso($"estribos: {b.Id} sin detalle: {e.Message}");
                }
            }
            else
                Registro.Paso($"estribos: {b.Id} no se ve en la vista, van solo sus textos");
            // Las notas no se pintan: van en negro siempre, aunque no se pidan los textos en negro (#313).
            double encabezado = arribaDetalle + AireNota + AltoEncabezado;
            TextNote.Create(doc, vista.Id, En(encabezado),
                TextosDespiece.EncabezadoEstribo(forma.Sum(x => x.Quantity), LectorDespiece.LargoBarra(b) * 0.3048, Gancho(b) * 0.3048),
                tipos.Nota(HorizontalTextAlignment.Center));
            TextNote.Create(doc, vista.Id, En(abajoDetalle - AireNota), TextosDespiece.Nombre(doc.GetElement(b.GetTypeId()).Name),
                tipos.Nota(HorizontalTextAlignment.Center));
            techo = Math.Max(techo, encabezado);
            piso = Math.Min(piso, abajoDetalle - AireNota - FilaEstribos);
            tope = piso - HuecoEstribos;
        }
        return (techo, piso);
    }

    /// <summary>
    /// El ancho debajo del estribo aparte y el alto a su izquierda, una vez cada uno, como la referencia de Santiago
    /// (decisión 14 del plan de #317). Por los tramos rectos de la barra más su diámetro, como el recubrimiento; una forma
    /// chata (grapa) solo lleva el ancho. <paramref name="centro"/> está en el eje vertical del detalle. Devuelve hasta dónde
    /// bajó, para poner el diámetro debajo.
    /// </summary>
    private static double AcotarEstribo(Document doc, View vista, Rebar b, XYZ centro, double arribaDetalle, double abajoDetalle, TiposPropios tipos)
    {
        XYZ arriba = vista.UpDirection, derecha = vista.RightDirection;
        double d = ((RebarBarType)doc.GetElement(b.GetTypeId())).BarNominalDiameter;
        List<XYZ> puntos = Curvas(b, true).SelectMany(c => c.Tessellate()).ToList();
        if (puntos.Count == 0)
            return abajoDetalle;
        double ancho = puntos.Max(p => p.DotProduct(derecha)) - puntos.Min(p => p.DotProduct(derecha)) + d;
        double alto = puntos.Max(p => p.DotProduct(arriba)) - puntos.Min(p => p.DotProduct(arriba)) + d;
        // El detalle puede tener ganchos que asoman: las cotas van por la barra, centradas en el detalle.
        double medio = (arribaDetalle + abajoDetalle) / 2;
        XYZ En(double x, double y) => centro + derecha * x + arriba * (y - centro.DotProduct(arriba));
        double linea = medio - alto / 2 - CotaDetalle;
        double lineaAncho = Math.Min(linea, abajoDetalle - CotaDetalle);
        // Las líneas de referencia van de la cota hacia el estribo (punto 14 de Santiago).
        CadenaPorPlanos(doc, vista, $"estribos: ancho de {b.Id}", En(-ancho / 2, lineaAncho), derecha, [0, ancho], tipos.CotaEstribo ?? tipos.Cota,
            arriba, medio - alto / 2 - lineaAncho);
        if (alto > FormaChata)
            CadenaPorPlanos(doc, vista, $"estribos: alto de {b.Id}", En(-ancho / 2 - CotaDetalle, medio - alto / 2), arriba, [0, alto], tipos.CotaEstribo ?? tipos.Cota,
                derecha, CotaDetalle);
        return Math.Min(linea, abajoDetalle - CotaDetalle);
    }

    /// <summary>
    /// Las formas de estribo de la viga o la columna, cada una con sus barras, la principal primero y las demás de más a
    /// menos tramos. Las que tienen el mismo diámetro, la misma cantidad de tramos y el mismo largo recto (±3 cm) son la
    /// misma forma: la grapa con el gancho grande de los traslapos se suma a la normal (decisión 16 del plan de #317). En
    /// cada forma van primero las barras de la variante con más piezas, que es la que se dibuja.
    /// </summary>
    internal static List<List<Rebar>> FormasEstribos(List<Rebar> estribos, Rebar principal)
    {
        double Recto(Rebar b) => Curvas(b, true).Sum(c => c.Length);
        int Tramos(Rebar b) => Curvas(b, true).Count;
        // Primero cada variante exacta (diámetro, largo y gancho al centímetro), la de más piezas primero.
        List<List<Rebar>> variantes = estribos
            .GroupBy(b => $"{b.GetTypeId()}|{Math.Round(LectorDespiece.LargoBarra(b) * 30.48)}|{Math.Round(Gancho(b) * 30.48)}")
            .Select(g => g.ToList()).OrderByDescending(v => v.Sum(b => b.Quantity)).ToList();
        var formas = new List<List<Rebar>>();
        foreach (List<Rebar> variante in variantes)
        {
            Rebar b = variante[0];
            List<Rebar>? misma = formas.FirstOrDefault(f => f[0].GetTypeId() == b.GetTypeId() && Tramos(f[0]) == Tramos(b)
                && Math.Abs(Recto(f[0]) - Recto(b)) <= MismoRecto);
            if (misma is null)
                formas.Add([.. variante]);
            else
            {
                Registro.Paso($"estribos: {variante.Sum(x => x.Quantity)} de {b.Id} se suman a la forma de {misma[0].Id}");
                misma.AddRange(variante);
            }
        }
        return formas.OrderByDescending(f => f.Contains(principal) ? int.MaxValue : Curvas(f[0], false).Count).ToList();
    }

    /// <summary>Dos estribos con el tramo recto así de parecido son la misma forma (decisión 16 del plan de #317).</summary>
    private const double MismoRecto = 0.03 * M;

    /// <summary>Una forma menos alta que esto (una grapa) no lleva la cota del alto.</summary>
    private const double FormaChata = 0.10 * M;

    /// <summary>De la barra del estribo aparte a la línea de su cota: deja lugar al número, que va encima de la línea.</summary>
    private static double CotaDetalle => 0.08 * M * Letra.Factor;

    /// <summary>
    /// Lo más a la izquierda del concreto de la viga continua, medido sobre <paramref name="sentido"/>: de ahí se cuenta el
    /// <c>x=</c> del corte (#314). Por los vértices de sus sólidos, no por la caja, que en una viga inclinada en planta es más
    /// grande que la viga.
    /// </summary>
    private static double Extremo(Document doc, VigaContinua viga, XYZ sentido)
    {
        var valores = new List<double>();
        foreach (Element e in viga.Vigas.Select(doc.GetElement))
        {
            foreach (GeometryObject g in e.get_Geometry(new Options()))
            {
                IEnumerable<Solid> solidos = g is GeometryInstance i ? i.GetInstanceGeometry().OfType<Solid>() : g is Solid s ? [s] : [];
                foreach (Solid solido in solidos.Where(x => x.Volume > 0))
                    foreach (Edge borde in solido.Edges)
                        valores.AddRange(borde.Tessellate().Select(p => p.DotProduct(sentido)));
            }
        }
        if (valores.Count > 0)
            return valores.Min();
        (XYZ min, XYZ max) = Caja(doc, viga);
        return Math.Min(min.DotProduct(sentido), max.DotProduct(sentido));
    }


    /// <summary>El largo del gancho que da Revit para esta barra (el más largo de sus dos puntas), en pies; 0 si no tiene.</summary>
    internal static double Gancho(Rebar b)
    {
        if (b.Document.GetElement(b.GetTypeId()) is not RebarBarType tipo)
            return 0;
        return new[] { 0, 1 }.Select(b.GetHookTypeId).Where(id => id != ElementId.InvalidElementId).Select(tipo.GetHookLength).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Ancho y alto con cotas de Revit, ligadas a las caras de la viga. El recubrimiento va en una cota en cadena contra
    /// planos de referencia (<see cref="CadenaPorPlanos"/>, #313): Revit ignora la referencia del estribo en una cota
    /// (probado el 30-oct, la cadena salió de un solo tramo). Si la cadena no se puede, va en texto como antes.
    /// </summary>
    private static void Acotar(Document doc, View vista, VigaContinua viga, Rebar? estribo, XYZ corte, XYZ derecha, double centroZ, TiposPropios tipos,
        (double Izq, double Der, double Abajo, double Arriba)? completa = null)
    {
        // Las referencias dejan de valer al crear una cota (la segunda fallaba con InternalException, 30-oct): se leen cada vez.
        List<PlanarFace> Lados() => Caras(doc.GetElement(viga.Vigas[0]), vista).Where(c => Math.Abs(c.FaceNormal.DotProduct(derecha)) > 0.99).OrderBy(c => c.Origin.DotProduct(derecha)).ToList();
        List<PlanarFace> Tapas() => Caras(doc.GetElement(viga.Vigas[0]), vista).Where(c => Math.Abs(c.FaceNormal.Z) > 0.99).OrderBy(c => c.Origin.Z).ToList();
        List<PlanarFace> lados = Lados(), tapas = Tapas();
        Registro.Paso("sección: caras horizontales " + string.Join(", ", tapas.Select(c => $"{c.Origin.Z * 0.3048:0.000}{(c.Reference is null ? " sin referencia" : "")}")));
        if (completa is null && (lados.Count < 2 || tapas.Count < 2))
        {
            Registro.Paso($"sección: sin cotas, {lados.Count} caras a los lados y {tapas.Count} horizontales");
            return;
        }
        // Con el contorno completo (decisión 3 del plan del 10-11) se acota ese contorno, contra planos de referencia: las
        // caras de Revit son las de la viga cortada por la losa.
        double cara = completa?.Izq ?? lados[0].Origin.DotProduct(derecha);
        double ancho = completa is { } r ? r.Der - r.Izq : (lados[^1].Origin - lados[0].Origin).DotProduct(derecha);
        double zAbajo = completa?.Abajo ?? tapas[0].Origin.Z, zArriba = completa?.Arriba ?? tapas[^1].Origin.Z;
        double alto = zArriba - zAbajo;
        // Sobre el plano del corte: el origen de una cara puede estar a metros de él, y ahí Revit no crea la cota (30-oct).
        XYZ enCorte = corte + derecha * (cara - corte.DotProduct(derecha));
        XYZ esquina = new(enCorte.X, enCorte.Y, zAbajo);

        // Cada intento en su SubTransaction: si Revit no acepta la cota se deshace solo eso, y la vista previa sigue (#300).
        bool Intentar(string nombre, string como, Func<IEnumerable<Reference>> referencias, XYZ desde, XYZ hacia)
        {
            using var intento = new SubTransaction(doc);
            intento.Start();
            try
            {
                var lista = new ReferenceArray();
                foreach (Reference r in referencias())
                    lista.Append(r ?? throw new InvalidOperationException("la cara no trae referencia"));
                Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(desde, hacia), lista);
                if (tipos.Cota is { } tipo)
                    cota.DimensionType = tipo;
                doc.Regenerate();
                intento.Commit();
                Registro.Paso($"sección: cota {nombre} {como}");
                return true;
            }
            catch (Exception e)
            {
                intento.RollBack();
                Registro.Paso($"sección: cota {nombre} {como} no se pudo: {e.Message}");
                return false;
            }
        }
        // Con la losa encima, la cara de arriba es la de la losa y no trae referencia (Invalid number of references, #300):
        // se acota contra planos de referencia en blanco puestos en esas mismas posiciones, como en CotarColumnas.
        IEnumerable<Reference> Planos(XYZ desde, XYZ hacia)
        {
            XYZ tramo = (hacia - desde).CrossProduct(vista.ViewDirection).Normalize() * 0.05 * M;
            var blanco = new OverrideGraphicSettings();
            blanco.SetProjectionLineColor(new Color(255, 255, 255));
            List<Reference> planos = new[] { desde, hacia }.Select(p =>
            {
                ReferencePlane plano = doc.Create.NewReferencePlane(p - tramo, p + tramo, vista.ViewDirection, vista);
                vista.SetElementOverrides(plano.Id, blanco);
                return plano.GetReference();
            }).ToList();
            doc.Regenerate();
            return planos;
        }
        void Cota(string nombre, Func<IEnumerable<Reference>> caras, XYZ desde, XYZ hacia)
        {
            if ((completa is not null || !Intentar(nombre, "con las caras de la viga", caras, desde, hacia)) && !Intentar(nombre, "con planos de referencia", () => Planos(desde, hacia), desde, hacia))
                Registro.Paso($"sección: la cota {nombre} queda sin crear");
        }
        // Primero (más cerca) la cadena del recubrimiento y más afuera la total, como en la columna (#282, #313).
        XYZ bajo = esquina - XYZ.BasisZ * TotalSeccion;
        Cota("del ancho", () => { List<PlanarFace> l = Lados(); return [l[0].Reference, l[^1].Reference]; }, bajo, bajo + derecha * ancho);
        XYZ alLado = esquina - derecha * TotalSeccion;
        Cota("del alto", () => { List<PlanarFace> t = Tapas(); return [t[0].Reference, t[^1].Reference]; }, alLado, alLado + XYZ.BasisZ * alto);

        if (estribo is null)
            return;
        // Por la cara exterior del estribo: su eje más medio diámetro, igual que la medida del detalle.
        double d = ((RebarBarType)doc.GetElement(estribo.GetTypeId())).BarNominalDiameter;
        // Por sus esquinas: el tramo donde arranca el gancho se pasa del lado contrario (6 mm en la viga 388501, y daba 0.18 · 0.03).
        IList<Curve> tramos = Curvas(estribo, true);
        List<Curve> esquinas = tramos.Where(c => c is Arc).ToList();
        List<XYZ> puntos = (esquinas.Count > 0 ? esquinas : tramos).SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) }).ToList();
        double desdeEje = puntos.Min(p => p.DotProduct(derecha)) - d / 2, hastaEje = puntos.Max(p => p.DotProduct(derecha)) + d / 2;
        double bajoEje = puntos.Min(p => p.Z) - d / 2, altoEje = puntos.Max(p => p.Z) + d / 2;
        // La cadena como cota de Revit (#313: "acotar las medidas de recubrimiento"), contra planos de referencia en blanco: la
        // referencia del estribo no sirve (30-oct). Si Revit no la acepta, queda el texto de antes en su lugar.
        XYZ cadenaAbajo = esquina - XYZ.BasisZ * CadenaSeccion;
        if (!CadenaPorPlanos(doc, vista, "sección: cadena del ancho", cadenaAbajo, derecha, [0, desdeEje - cara, hastaEje - cara, ancho], tipos.Cota))
            TextNote.Create(doc, vista.Id, cadenaAbajo + derecha * (ancho / 2),
                Cadena(desdeEje - cara, hastaEje - desdeEje, cara + ancho - hastaEje), tipos.Nota(HorizontalTextAlignment.Center));
        XYZ cadenaLado = esquina - derecha * CadenaSeccion;
        if (!CadenaPorPlanos(doc, vista, "sección: cadena del alto", cadenaLado, XYZ.BasisZ, [0, bajoEje - zAbajo, altoEje - zAbajo, alto], tipos.Cota))
        {
            TextNoteOptions vertical = tipos.Nota(HorizontalTextAlignment.Center);
            vertical.Rotation = Math.PI / 2;
            TextNote.Create(doc, vista.Id, new XYZ(cadenaLado.X, cadenaLado.Y, centroZ), Cadena(bajoEje - zAbajo, altoEje - bajoEje, zArriba - altoEje), vertical);
        }
    }

    /// <summary>
    /// Una cota en cadena de Revit contra planos de referencia en blanco, uno en cada posición de
    /// <paramref name="posiciones"/> (en pies, sobre <paramref name="eje"/> desde <paramref name="origen"/>). Sirve donde
    /// no hay caras que acotar, como la cara exterior del estribo (#313). En su SubTransaction: si Revit no la acepta se
    /// deshace solo eso y devuelve false. Con <paramref name="haciaDibujo"/> (lo que se acota está a ese lado de la línea,
    /// a <paramref name="hastaDibujo"/> pies) cada plano va de la línea de cota hacia el dibujo, y no centrado en ella:
    /// centrado, la línea de referencia asomaba hacia afuera (punto 14 de Santiago, paso 3 del plan del 10-11).
    /// </summary>
    internal static bool CadenaPorPlanos(Document doc, View vista, string nombre, XYZ origen, XYZ eje, IReadOnlyList<double> posiciones, DimensionType? tipo,
        XYZ? haciaDibujo = null, double hastaDibujo = 0)
    {
        using var intento = new SubTransaction(doc);
        intento.Start();
        try
        {
            XYZ tramo = eje.CrossProduct(vista.ViewDirection).Normalize() * 0.05 * M;
            var blanco = new OverrideGraphicSettings();
            blanco.SetProjectionLineColor(new Color(255, 255, 255));
            var lista = new ReferenceArray();
            foreach (double posicion in posiciones)
            {
                XYZ p = origen + eje * posicion;
                (XYZ desde, XYZ hasta) = haciaDibujo is { } h && hastaDibujo > 0
                    ? (p + h.Normalize() * (hastaDibujo * PlanoDesde), p + h.Normalize() * hastaDibujo)
                    : (p - tramo, p + tramo);
                ReferencePlane plano = doc.Create.NewReferencePlane(desde, hasta, vista.ViewDirection, vista);
                vista.SetElementOverrides(plano.Id, blanco);
                lista.Append(plano.GetReference());
            }
            doc.Regenerate();
            Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(origen + eje * posiciones[0], origen + eje * posiciones[^1]), lista);
            if (tipo is not null)
                cota.DimensionType = tipo;
            doc.Regenerate();
            intento.Commit();
            Registro.Paso($"{nombre}: cota en cadena de {cota.NumberOfSegments} tramos: "
                + string.Join(" | ", cota.Segments.Cast<DimensionSegment>().Select(s => s.ValueString)));
            return true;
        }
        catch (Exception e)
        {
            intento.RollBack();
            Registro.Paso($"{nombre}: cota en cadena no se pudo: {e.Message}");
            return false;
        }
    }

    /// <summary>Dónde empieza el plano hacia el dibujo, como parte de la distancia de la línea de cota al dibujo.</summary>
    private const double PlanoDesde = 0.3;

    internal static string Cadena(params double[] pies) => string.Join(" · ", pies.Select(p => (p * 0.3048).ToString("0.00", CultureInfo.InvariantCulture)));

    /// <summary>Las caras planas de la viga con referencia, para acotarlas en esta vista.</summary>
    internal static List<PlanarFace> Caras(Element viga, View vista)
    {
        var caras = new List<PlanarFace>();
        foreach (GeometryObject g in viga.get_Geometry(new Options { ComputeReferences = true, View = vista }))
        {
            IEnumerable<Solid> solidos = g is GeometryInstance i ? i.GetInstanceGeometry().OfType<Solid>() : g is Solid s ? [s] : [];
            foreach (Solid solido in solidos.Where(x => x.Volume > 0))
                caras.AddRange(solido.Faces.OfType<PlanarFace>());
        }
        return caras;
    }

    internal static IList<Curve> Curvas(Rebar b, bool sinGanchos) => b.GetCenterlineCurves(false, sinGanchos, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);

    /// <summary>
    /// Dónde cortar la viga: en una luz libre (fuera de las columnas y lejos de sus caras), sin caer en un traslapo, donde
    /// las barras salen dobles y la sección dejaría de parecerse a la del plano, y lo más cerca del centro de la primera
    /// luz. Antes se buscaba en la primera viga sin mirar las columnas, y el corte cayó a 0.075 m del extremo, dentro de la
    /// columna: la viga salía de canto, como un cuadro negro (#314, decisión 21 del plan de #317).
    /// </summary>
    private static double PuntoDeCorte(Document doc, VigaContinua viga, List<Rebar> barras, XYZ dir, XYZ min, XYZ max)
    {
        // Por el concreto, no por la caja de Revit: en la viga de cimentación (V C, 09-nov) la caja sale más larga que el
        // concreto, y la "primera luz" quedaba fuera de la viga (decisión 28 del plan de #317).
        double a = Extremo(doc, viga, dir), b = -Extremo(doc, viga, dir.Negate());
        double cajaA = Math.Min(min.DotProduct(dir), max.DotProduct(dir)), cajaB = Math.Max(min.DotProduct(dir), max.DotProduct(dir));
        // Si los sólidos no se pudieron leer bien (menos de la mitad del largo de la caja), la caja.
        if (b - a < (cajaB - cajaA) / 2)
        {
            Registro.Paso($"sección: el concreto mide {(b - a) * 0.3048:0.00} y la caja {(cajaB - cajaA) * 0.3048:0.00}; se usa la caja");
            (a, b) = (cajaA, cajaB);
        }
        List<(double Desde, double Hasta)> columnas = Apoyos(doc, viga, dir, min, max).Select(c => c.get_BoundingBox(null)).Where(c => c is not null)
            .Select(c => (Math.Min(c.Min.DotProduct(dir), c.Max.DotProduct(dir)), Math.Max(c.Min.DotProduct(dir), c.Max.DotProduct(dir)))).ToList();
        List<TramoViga> luces = TramosViga.Calcular(a, b, columnas).Where(t => !t.EsColumna && t.Hasta - t.Desde > 2 * LejosDeLaColumna).ToList();
        if (luces.Count == 0)
            luces = [new TramoViga(a, b, false)];
        // Donde hay concreto de verdad, por una línea al medio de la viga: en la de cimentación las zapatas le cortan
        // tramos enteros y el corte caía en uno de ellos (viga "mocha", decisión 33 del plan de #317).
        // Cinco líneas: el centro y cerca de cada borde de la sección. En la viga de cimentación la zapata le come parte del
        // ancho, y con solo el centro la sección salía con una franja cortada y el resto de fondo (13:10, decisión 36).
        XYZ medio = (min + max) / 2, ancho = dir.CrossProduct(XYZ.BasisZ).Normalize();
        double medioAncho = Math.Abs((max - min).DotProduct(ancho)) / 2 * 0.7, medioAlto = (max.Z - min.Z) / 2 * 0.7;
        List<(double Desde, double Hasta)> concreto = new[] { XYZ.Zero, ancho * medioAncho, -ancho * medioAncho, XYZ.BasisZ * medioAlto, -XYZ.BasisZ * medioAlto }
            .Select(d => ConcretoAlMedio(doc, viga, dir, medio + d, a, b)).Aggregate(Interseccion);
        bool HayConcreto(double p) => concreto.Count == 0 || concreto.Any(c => p >= c.Desde + LejosDeLaColumna && p <= c.Hasta - LejosDeLaColumna);
        double centro = (luces[0].Desde + luces[0].Hasta) / 2;
        List<(double Desde, double Hasta, int Cantidad)> corridas = barras.Where(x => EsLongitudinal(x, dir) && !MarcaVigas.EsBaston(x)).Select(x =>
        {
            Curve mayor = Mayor(x);
            double p0 = mayor.GetEndPoint(0).DotProduct(dir), p1 = mayor.GetEndPoint(1).DotProduct(dir);
            return (Math.Min(p0, p1), Math.Max(p0, p1), x.Quantity);
        }).ToList();
        double margen(TramoViga t) => Math.Min(LejosDeLaColumna, (t.Hasta - t.Desde) / 4);
        List<double> candidatos = luces.SelectMany(t => Enumerable.Range(0, 41).Select(i => t.Desde + margen(t) + (t.Hasta - t.Desde - 2 * margen(t)) * i / 40.0)).ToList();
        if (candidatos.Any(HayConcreto))
            candidatos = candidatos.Where(HayConcreto).ToList();
        else
            Registro.Paso("sección: ningún punto de las luces cae en concreto entero; se corta igual");
        Registro.Paso("sección: concreto al medio " + string.Join(", ", concreto.Select(c => $"{(c.Desde - a) * 0.3048:0.00}-{(c.Hasta - a) * 0.3048:0.00}")));
        // Con un estribo a la vista: la sección mira 0.30 m hacia un lado (Seccion), y si ahí no hay estribo salen sin él la
        // cadena del recubrimiento y el estribo aparte (V C, 13:10, decisión 35 del plan de #317).
        XYZ haciaAtras = dir.CrossProduct(XYZ.BasisZ).Negate().CrossProduct(XYZ.BasisZ).Negate();
        double sentido = Math.Sign(haciaAtras.DotProduct(dir));
        List<double> estribos = barras.Where(x => !EsLongitudinal(x, dir)).SelectMany(x => Enumerable.Range(0, x.NumberOfBarPositions).Select(i =>
            x.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, i).SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) })
                .Select(q => q.DotProduct(dir)).DefaultIfEmpty(double.NaN).Average())).Where(q => !double.IsNaN(q)).ToList();
        bool VeEstribo(double p) => estribos.Any(q => (q - p) * sentido is var d && d >= 0.01 * M && d <= FondoSeccion - 0.02 * M);
        if (candidatos.Any(VeEstribo))
            candidatos = candidatos.Where(VeEstribo).ToList();
        else
            Registro.Paso($"sección: ningún punto ve un estribo ({estribos.Count} estribos)");
        double elegido = candidatos
            .OrderBy(p => corridas.Where(c => p >= c.Desde && p <= c.Hasta).Sum(c => c.Cantidad))
            .ThenBy(p => Math.Abs(p - centro))
            .First();
        Registro.Paso($"sección: concreto de {a * 0.3048:0.00} a {b * 0.3048:0.00} (caja {Math.Min(min.DotProduct(dir), max.DotProduct(dir)) * 0.3048:0.00} a {Math.Max(min.DotProduct(dir), max.DotProduct(dir)) * 0.3048:0.00}); luces " + string.Join(", ", luces.Select(t => $"{(t.Desde - a) * 0.3048:0.00}-{(t.Hasta - a) * 0.3048:0.00}")) + $"; corte a {(elegido - a) * 0.3048:0.00} del extremo");
        return elegido;
    }

    /// <summary>
    /// Los tramos de la viga continua donde una línea por el medio de su sección atraviesa concreto, sobre
    /// <paramref name="dir"/>. Con <c>Solid.IntersectWithCurve</c> (API pública). Vacío si Revit no lo pudo calcular.
    /// </summary>
    private static List<(double Desde, double Hasta)> ConcretoAlMedio(Document doc, VigaContinua viga, XYZ dir, XYZ medio, double a, double b)
    {
        var tramos = new List<(double Desde, double Hasta)>();
        try
        {
            XYZ eje = medio - dir * medio.DotProduct(dir);
            Line linea = Line.CreateBound(eje + dir * (a - 1 * M), eje + dir * (b + 1 * M));
            foreach (Element e in viga.Vigas.Select(doc.GetElement))
                foreach (GeometryObject g in e.get_Geometry(new Options()))
                {
                    IEnumerable<Solid> solidos = g is GeometryInstance i ? i.GetInstanceGeometry().OfType<Solid>() : g is Solid s ? [s] : [];
                    foreach (Solid solido in solidos.Where(x => x.Volume > 0))
                    {
                        SolidCurveIntersection cruce = solido.IntersectWithCurve(linea, new SolidCurveIntersectionOptions());
                        for (int k = 0; k < cruce.SegmentCount; k++)
                        {
                            Curve c = cruce.GetCurveSegment(k);
                            double p0 = c.GetEndPoint(0).DotProduct(dir), p1 = c.GetEndPoint(1).DotProduct(dir);
                            tramos.Add((Math.Min(p0, p1), Math.Max(p0, p1)));
                        }
                    }
                }
        }
        catch (Exception e)
        {
            Registro.Paso("sección: no se pudo medir el concreto al medio: " + e.Message);
            tramos.Clear();
        }
        return tramos.OrderBy(t => t.Desde).ToList();
    }

    /// <summary>
    /// Donde empieza a la izquierda el dibujo de la vista larga y su cota del largo total: lo primero entre la caja de la viga
    /// y sus columnas, como en <see cref="CotarColumnas"/>.
    /// </summary>
    private static double InicioDelDibujo(Document doc, VigaContinua viga, XYZ derecha, XYZ min, XYZ max)
    {
        double a = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), b = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
        List<(double Desde, double Hasta)> columnas = Apoyos(doc, viga, derecha, min, max).Select(c => c.get_BoundingBox(null)).Where(c => c is not null)
            .Select(c => (Math.Min(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)), Math.Max(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)))).ToList();
        return TramosViga.Calcular(a, b, columnas)[0].Desde;
    }

    /// <summary>Los tramos comunes a dos listas de tramos; si una está vacía (no se pudo medir), la otra.</summary>
    private static List<(double Desde, double Hasta)> Interseccion(List<(double Desde, double Hasta)> uno, List<(double Desde, double Hasta)> otro) =>
        uno.Count == 0 ? otro : otro.Count == 0 ? uno
        : uno.SelectMany(x => otro.Select(y => (Desde: Math.Max(x.Desde, y.Desde), Hasta: Math.Min(x.Hasta, y.Hasta)))).Where(t => t.Hasta > t.Desde).OrderBy(t => t.Desde).ToList();

    /// <summary>Lo que la sección de la viga ve detrás del corte: alcanza para un estribo.</summary>
    private const double FondoSeccion = 0.3 * M;

    /// <summary>El corte de la sección queda por lo menos así de lejos de las caras de las columnas (decisión 21).</summary>
    private const double LejosDeLaColumna = 0.15 * M;

    internal static ViewSection Preparar(Document doc, ViewFamilyType tipo, BoundingBoxXYZ caja, string prefijo, int escala)
    {
        ViewSection vista = ViewSection.CreateSection(doc, tipo.Id, caja);
        vista.Name = $"CQEING temporal {prefijo} {Guid.NewGuid():N}";
        vista.ViewTemplateId = ElementId.InvalidElementId;
        vista.Scale = escala;
        vista.DetailLevel = ViewDetailLevel.Medium;
        vista.DisplayStyle = DisplayStyle.Wireframe;
        vista.CropBoxActive = true;
        vista.CropBoxVisible = false;
        doc.Regenerate();
        return vista;
    }

    /// <summary>
    /// Recorta alrededor de un punto del modelo. Se pasa a las coordenadas del recorte porque Revit, al crear la sección,
    /// le pone el origen en una esquina y no en el que se le pidió.
    /// </summary>
    internal static void Recortar(View vista, XYZ punto, double izquierda, double derecha, double abajo, double arriba)
    {
        BoundingBoxXYZ recorte = vista.CropBox;
        XYZ p = recorte.Transform.Inverse.OfPoint(punto);
        recorte.Min = new XYZ(p.X - izquierda, p.Y - abajo, recorte.Min.Z);
        recorte.Max = new XYZ(p.X + derecha, p.Y + arriba, recorte.Max.Z);
        vista.CropBox = recorte;
    }

    /// <summary>
    /// Solo la viga, su acero y lo que se pida en <paramref name="tambien"/> (las columnas de los nudos en la vista larga,
    /// #262): sin rellenos ni dibujos que imiten otra cosa (#217). El acero de lo demás queda oculto.
    /// </summary>
    private static void Limpiar(Document doc, View vista, VigaContinua viga, List<Rebar> barras, IEnumerable<ElementId>? tambien = null)
    {
        var deja = new HashSet<ElementId>(barras.Select(b => b.Id).Concat(viga.Vigas).Concat(tambien ?? []));
        List<ElementId> ocultar = new FilteredElementCollector(doc, vista.Id).WhereElementIsNotElementType()
            .Where(e => e.Category is { CategoryType: CategoryType.Model } && !deja.Contains(e.Id) && e.CanBeHidden(vista))
            .Select(e => e.Id).ToList();
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
        Importados.Ocultar(doc, vista);
        vista.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true);
        vista.SetCategoryHidden(new ElementId(BuiltInCategory.OST_SectionBox), true);
    }

    /// <summary>
    /// La viga con su contorno entero aunque algo le corte el concreto (Luciano, 09-nov: en la de cimentación las zapatas
    /// la dejaban en pedazos grises; "que el borde saliera como si estuviera completa la viga", decisión 40 del plan de
    /// #317). La viga de Revit no se dibuja (líneas en blanco, sin relleno) y va su contorno en líneas de detalle negras:
    /// arriba y abajo en cada luz entre columnas, y los extremos que no caen en una columna.
    /// </summary>
    private static void ContornoCompleto(Document doc, View vista, VigaContinua viga, XYZ derecha, XYZ enPlano, XYZ min, XYZ max, List<ElementId> apoyos)
    {
        try
        {
            var invisible = new OverrideGraphicSettings();
            invisible.SetProjectionLineColor(new Color(255, 255, 255));
            invisible.SetCutLineColor(new Color(255, 255, 255));
            invisible.SetSurfaceForegroundPatternVisible(false);
            invisible.SetSurfaceBackgroundPatternVisible(false);
            invisible.SetCutForegroundPatternVisible(false);
            invisible.SetCutBackgroundPatternVisible(false);
            invisible.SetSurfaceTransparency(100);
            foreach (ElementId id in viga.Vigas)
                vista.SetElementOverrides(id, invisible);

            double a = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), b = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
            List<(double Desde, double Hasta)> columnas = apoyos.Select(id => doc.GetElement(id).get_BoundingBox(null)).Where(c => c is not null)
                .Select(c => (Math.Min(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)), Math.Max(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)))).ToList();
            IReadOnlyList<TramoViga> tramos = TramosViga.Calcular(a, b, columnas);
            XYZ En(double x, double z) => new XYZ(enPlano.X, enPlano.Y, z) + derecha * (x - enPlano.DotProduct(derecha));
            var lineas = new List<Line>();
            foreach (TramoViga t in tramos.Where(t => !t.EsColumna && t.Hasta - t.Desde > doc.Application.ShortCurveTolerance))
            {
                double desde = Math.Max(t.Desde, a), hasta = Math.Min(t.Hasta, b);
                if (hasta - desde <= doc.Application.ShortCurveTolerance)
                    continue;
                lineas.Add(Line.CreateBound(En(desde, max.Z), En(hasta, max.Z)));
                lineas.Add(Line.CreateBound(En(desde, min.Z), En(hasta, min.Z)));
            }
            // Los extremos de la viga que no quedan dentro de una columna.
            foreach (double x in new[] { a, b }.Where(x => !columnas.Any(c => x >= c.Desde - 0.01 * M && x <= c.Hasta + 0.01 * M)))
                lineas.Add(Line.CreateBound(En(x, min.Z), En(x, max.Z)));
            var negro = new OverrideGraphicSettings();
            negro.SetProjectionLineColor(new Color(0, 0, 0));
            negro.SetProjectionLineWeight(3);
            foreach (Line l in lineas)
                vista.SetElementOverrides(doc.Create.NewDetailCurve(vista, l).Id, negro);
            Registro.Paso($"longitudinal: contorno de la viga con {lineas.Count} líneas");
        }
        catch (Exception e)
        {
            Registro.Paso("longitudinal: contorno de la viga no se pudo: " + e.Message);
        }
    }

    /// <summary>
    /// Las columnas de los nudos con un relleno gris liso y su contorno en negro, sin rayados ni texturas (punto 12 de
    /// Santiago, decisión 26 del plan de #317; antes iban solo con el contorno, decisión 9 del plan de #266). El acero de la
    /// viga se sigue viendo porque se dibuja por encima (SetUnobscuredInView en Colorear).
    /// </summary>
    private static void SoloContorno(View vista, List<ElementId> elementos)
    {
        ElementId solido = Solido(vista.Document);
        var gris = new Color(205, 205, 205);
        var contorno = new OverrideGraphicSettings();
        contorno.SetProjectionLineColor(new Color(0, 0, 0));
        contorno.SetCutLineColor(new Color(0, 0, 0));
        contorno.SetSurfaceForegroundPatternId(solido);
        contorno.SetSurfaceForegroundPatternColor(gris);
        contorno.SetSurfaceBackgroundPatternVisible(false);
        contorno.SetCutForegroundPatternId(solido);
        contorno.SetCutForegroundPatternColor(gris);
        contorno.SetCutBackgroundPatternVisible(false);
        foreach (ElementId id in elementos)
            vista.SetElementOverrides(id, contorno);
    }

    internal static void Colorear(Document doc, View vista, List<Rebar> barras)
    {
        foreach (Rebar b in barras)
        {
            b.SetUnobscuredInView(vista, true);
            Pintar(vista, b.Id, b, null);
        }
    }

    internal static void Pintar(View vista, ElementId id, Rebar barra, int? grosor)
    {
        if (textosNegros && barra.Document.GetElement(id) is TextNote)
            return;
        (byte r, byte g, byte a) = ColoresDiametro.Rgb(LectorDespiece.Numero(barra.Document.GetElement(barra.GetTypeId()).Name));
        var color = new Color(r, g, a);
        var o = new OverrideGraphicSettings();
        o.SetProjectionLineColor(color);
        o.SetCutLineColor(color);
        o.SetSurfaceForegroundPatternId(Solido(barra.Document));
        o.SetSurfaceForegroundPatternColor(color);
        o.SetCutForegroundPatternId(Solido(barra.Document));
        o.SetCutForegroundPatternColor(color);
        if (grosor is { } g2)
            o.SetProjectionLineWeight(g2);
        vista.SetElementOverrides(id, o);
    }

    internal static ElementId Solido(Document doc) => new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
        .First(f => f.GetFillPattern().IsSolidFill).Id;

    private static string NombreBarra(Document doc, Rebar b)
    {
        string tipo = doc.GetElement(b.GetTypeId()).Name;
        int n = LectorDespiece.Numero(tipo);
        return n > 0 ? "#" + n : tipo;
    }

    private static XYZ Centro(Rebar b)
    {
        BoundingBoxXYZ c = b.get_BoundingBox(null);
        return (c.Min + c.Max) / 2;
    }

    /// <summary>El punto medio entre la primera y la última barra del conjunto.</summary>
    private static XYZ CentroDelConjunto(Rebar b)
    {
        XYZ punto = Mayor(b).GetEndPoint(0);
        RebarShapeDrivenAccessor acceso = b.GetShapeDrivenAccessor();
        return (acceso.GetBarPositionTransform(0).OfPoint(punto) + acceso.GetBarPositionTransform(b.Quantity - 1).OfPoint(punto)) / 2;
    }

    internal static Curve Mayor(Rebar b) => b.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0).OrderByDescending(c => c.Length).First();

    /// <summary>Longitudinal si su tramo más largo va a lo largo de la viga. No se mira el nombre de la forma, que depende del proyecto.</summary>
    private static bool EsLongitudinal(Rebar b, XYZ dir)
    {
        Curve mayor = Mayor(b);
        XYZ d = (mayor.GetEndPoint(1) - mayor.GetEndPoint(0)).Normalize();
        return Math.Abs(d.DotProduct(dir)) > 0.99;
    }

    /// <summary>La dirección para que los ejes se lean en orden, de izquierda a derecha (A-B-C, 1-2-3).</summary>
    private static XYZ SentidoDeLectura(Document doc, VigaContinua viga)
    {
        XYZ dir = viga.Direccion;
        List<(double Pos, string Nombre)> cruzan = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>()
            .Where(g => g.Curve is Line l && Math.Abs(l.Direction.DotProduct(dir)) < 0.01)
            .Select(g => (g.Curve.GetEndPoint(0).DotProduct(dir), g.Name)).OrderBy(c => c.Item1).ToList();
        if (cruzan.Count >= 2 && string.CompareOrdinal(cruzan[0].Nombre, cruzan[^1].Nombre) > 0)
            return dir.Negate();
        return dir;
    }
}

/// <summary>Los tipos propios del despiece. Nunca se editan los del proyecto: se duplican (#217).</summary>
internal sealed class TiposPropios
{
    private readonly ElementId notaId;

    private TiposPropios(RebarBendingDetailType barra, RebarBendingDetailType barraSinMedidas, DimensionType? cota, DimensionType? cotaCorta, DimensionType? cotaEstribo, ElementId notaId)
    {
        Barra = barra;
        BarraSinMedidas = barraSinMedidas;
        Cota = cota;
        CotaCorta = cotaCorta;
        CotaEstribo = cotaEstribo;
        this.notaId = notaId;
    }

    public RebarBendingDetailType Barra { get; }

    /// <summary>Para las barras longitudinales: su largo va en la etiqueta y no se repite en el dibujo.</summary>
    public RebarBendingDetailType BarraSinMedidas { get; }

    public DimensionType? Cota { get; }

    /// <summary>En metros y con las líneas de referencia cortas, para que no crucen las barras que hay entre la cota y la viga.</summary>
    public DimensionType? CotaCorta { get; }

    /// <summary>
    /// Para el estribo aparte: la línea de referencia de largo fijo, de la cota hacia el estribo, y nada hacia afuera
    /// (punto 14 de Santiago, paso 3 del plan del 10-11). Con la normal, el estribo queda más cerca que el espacio que
    /// Revit deja entre la línea de referencia y lo acotado, y solo se veía lo que sobresale hacia afuera (medido el 10-11).
    /// </summary>
    public DimensionType? CotaEstribo { get; }

    public TextNoteOptions Nota(HorizontalTextAlignment alineacion) => new(notaId) { HorizontalAlignment = alineacion };

    /// <summary>
    /// <paramref name="definitivos"/>: para vistas que se quedan en el proyecto, con nombre fijo y sin repetirlos si ya
    /// están. Si no, con nombre único, porque la vista previa los deshace.
    /// </summary>
    public static TiposPropios Crear(Document doc, bool definitivos)
    {
        string sufijo = Guid.NewGuid().ToString("N");
        // Un tipo por tamaño de letra: crear vistas con otro tamaño no cambia las ya creadas (#261, decisión 4).
        string Nombre(string fijo) => definitivos ? Letra.Nombre(fijo) : "CQEING temporal " + sufijo;
        List<RebarBendingDetailType> todos = new FilteredElementCollector(doc).OfClass(typeof(RebarBendingDetailType)).Cast<RebarBendingDetailType>().ToList();
        // El que no trae etiqueta: la del proyecto dice "#5 CQEING - 34", y borrarla después borra también el detalle.
        RebarBendingDetailType original = todos.FirstOrDefault(t => !TieneEtiqueta(doc, t)) ?? todos.First();
        string nombreBarra = Nombre("CQ Despiece sin etiqueta");
        var barra = todos.FirstOrDefault(t => t.Name == nombreBarra) ?? (RebarBendingDetailType)original.Duplicate(nombreBarra);
        // Cerrado, como lo dibuja Revit por defecto (#313, Santiago 09-nov): el desplegado del 31-oct (Juan) sacaba el gancho
        // para afuera. Se fija siempre: los tipos que ya están en un proyecto pueden haber quedado en "Desplegado".
        barra.SegmentsRepresentation = BendingDetailSegmentsRepresentation.Standard;
        // Sin la medida de los ganchos en el detalle: con el número legible se encimaban sobre los ganchos. Su largo va en
        // el encabezado del estribo, G= (ApilarEstribos, #313).
        barra.SegmentLengthDimensionsForHooksEnabled = false;
        // Sin ninguna medida: el detalle va en el color de su diámetro y sus medidas en negro, puestas aparte (decisión 14 del
        // plan de #317).
        barra.SegmentLengthDimensionsEnabled = false;
        DimensionType? metros = null;
        foreach (Parameter p in barra.Parameters)
        {
            if (p.IsReadOnly || p.StorageType != StorageType.ElementId)
                continue;
            if (doc.GetElement(p.AsElementId()) is DimensionType { StyleType: DimensionStyleType.Linear } lineal)
            {
                Registro.Paso("tipos: cota en metros " + p.Definition.Name);
                metros ??= EnMetros(doc, lineal, Nombre("CQ Despiece - metros"));
                p.Set(metros.Id);
            }
        }
        Registro.Paso("tipos: nota de texto");
        List<TextNoteType> notas = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().ToList();
        string nombreNota = Nombre("CQ Despiece etiqueta");
        TextNoteType? nota = notas.FirstOrDefault(n => n.Name == nombreNota);
        if (nota is null)
        {
            nota = (TextNoteType)notas[0].Duplicate(nombreNota);
            nota.get_Parameter(BuiltInParameter.TEXT_SIZE).Set(Letra.Pies);
            nota.get_Parameter(BuiltInParameter.TEXT_BACKGROUND).Set(1);
        }
        DimensionType? corta = null;
        if (metros is not null)
        {
            corta = EnMetros(doc, metros, Nombre("CQ Despiece - metros") + " corta");
            // 1 = línea de referencia fija, medida desde la línea de cota; 0 = hasta el elemento.
            corta.get_Parameter(BuiltInParameter.DIM_WITNS_LINE_CNTRL).Set(1);
            corta.get_Parameter(BuiltInParameter.DIM_WITNS_LINE_EXTENSION_BELOW).Set(0.0024 / 0.3048);
        }
        DimensionType? estribo = null;
        if (metros is not null)
        {
            estribo = EnMetros(doc, metros, Nombre("CQ Despiece - metros") + " estribo");
            estribo.get_Parameter(BuiltInParameter.DIM_WITNS_LINE_CNTRL).Set(1);
            estribo.get_Parameter(BuiltInParameter.DIM_WITNS_LINE_EXTENSION_BELOW).Set(0.0024 / 0.3048);
            // Lo que la línea de referencia sigue más allá de la línea de cota, hacia afuera: nada.
            if (estribo.get_Parameter(BuiltInParameter.WITNS_LINE_EXTENSION) is { IsReadOnly: false } afuera)
                afuera.Set(0.0);
        }
        string nombreSinMedidas = Nombre("CQ Despiece sin etiqueta") + " sin medidas";
        var sinMedidas = todos.FirstOrDefault(t => t.Name == nombreSinMedidas) ?? (RebarBendingDetailType)barra.Duplicate(nombreSinMedidas);
        sinMedidas.SegmentLengthDimensionsEnabled = false;
        // Sale del de estribos, pero las barras longitudinales se dibujan como en la viga.
        sinMedidas.SegmentsRepresentation = BendingDetailSegmentsRepresentation.Standard;
        return new TiposPropios(barra, sinMedidas, metros, corta, estribo, nota.Id);
    }

    private static bool TieneEtiqueta(Document doc, RebarBendingDetailType tipo) => tipo.Parameters.Cast<Parameter>()
        .Any(p => p.StorageType == StorageType.ElementId && doc.GetElement(p.AsElementId())?.Category?.Id.Value == (long)BuiltInCategory.OST_RebarTags);

    private static DimensionType EnMetros(Document doc, DimensionType original, string nombre)
    {
        if (original.Name == nombre)
            return ConTextoLegible(original);
        if (new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>().FirstOrDefault(d => d.Name == nombre) is { } existente)
            return ConTextoLegible(existente);
        var copia = (DimensionType)original.Duplicate(nombre);
        copia.SetUnitsFormatOptions(new FormatOptions(UnitTypeId.Meters) { UseDefault = false, Accuracy = 0.01, SuppressTrailingZeros = false });
        return ConTextoLegible(copia);
    }

    /// <summary>
    /// El número de la cota del mismo tamaño que las notas (el elegido en el tablero, #261): el tipo duplicado hereda el del proyecto, que puede
    /// ser diminuto (hallazgo 35 del plan de columnas). Es un tipo del despiece: no se toca ninguno del proyecto.
    /// </summary>
    private static DimensionType ConTextoLegible(DimensionType tipo)
    {
        Parameter? tamano = tipo.get_Parameter(BuiltInParameter.TEXT_SIZE);
        if (tamano is null || tamano.IsReadOnly)
            return tipo;
        Registro.Paso($"tipos: texto de cota de «{tipo.Name}» {tamano.AsDouble() * 304.8:0.00} mm");
        if (Math.Abs(tamano.AsDouble() - Letra.Pies) > 1e-6)
            tamano.Set(Letra.Pies);
        return tipo;
    }
}
