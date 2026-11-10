using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Despiece.Calculo;

namespace CQEING.Despiece.Revit.Columnas;

/// <summary>
/// La vista previa de una columna (#227): el alzado con su acero, armado como dice la decisión 14 del plan
/// docs/planes/2026-10-31-despiece-columnas-226-228.md. Lo que en la viga va arriba y abajo, aquí va a los lados: las
/// barras sacadas a la derecha, cada una a su altura real; los estribos y las cotas a la izquierda.
/// </summary>
internal static class GeneradorColumna
{
    private const double M = 1 / 0.3048;

    /// <summary>La que se eligió en el tablero, como la viga (#272).</summary>
    private static int Escala => Letra.EscalaAlzado;

    /// <summary>El recorte deja ver esto a cada lado de la columna: el arranque de sus vigas.</summary>
    private const double Lado = 0.30 * M;
    private const double Holgura = 0.005 * M;

    /// <summary>De la cara derecha de la columna al primer carril de barras sacadas (decisión 13 del plan de #317).</summary>
    private static double InicioSacadas => 0.02 * M * Letra.Papel;

    /// <summary>Entre dos piezas del mismo carril (una encima de otra) y entre carriles.</summary>
    // Más pegadas (decisión 25 del plan de #287 y 11 del de #317): a 1:50 los huecos medidos a 1:15 crecen 3.3 veces.
    private static double HuecoEnCarril => 0.05 * M * Letra.Papel;
    private static double HuecoCarriles => 0;

    /// <summary>Entre el grupo de barras con el gancho a la derecha y el de la izquierda (#277): "un pequeño espacio".</summary>
    private static double HuecoGrupos => 0.02 * M * Letra.Papel;

    /// <summary>Lo que se corre a un lado la pieza que traslapa con la de abajo, como en la viga.</summary>
    private static double DesfaseTraslapo => 0.05 * M * Letra.Papel;

    // A 1:15 la caja de la nota baja 0.09 m desde su punto y mide 0.12 m (medido en la viga, 30-oct).
    private static double AltoTexto => 0.11 * M * Letra.Factor;
    private static double BajaNota => 0.09 * M * Letra.Factor;
    private static double AireNota => 0.01 * M * Letra.Papel;
    // El grueso de una nota girada: el alto de la letra en el modelo más su margen (decisión 26 del plan de #287). Con
    // 0.10 m × Factor salía casi el doble y la medida del traslapo quedaba lejos de su barra.
    private static double AnchoNotaGirada => Letra.Mm / 1000 * Letra.Escala * M * 1.3;
    private static double FilaEstribos => 0.13 * M * Letra.Factor;
    private const double MismaZona = 0.10 * M;

    /// <summary>De la etiqueta de estribos al borde izquierdo del recorte.</summary>
    private static double AireEstribos => 0.05 * M * Letra.Papel;

    /// <summary>A la izquierda del recorte: la cadena de elementos y, más afuera, el alto total.</summary>
    private static double CotaElementos => 0.42 * M * Letra.Factor;
    private static double CotaTotal => 0.52 * M * Letra.Factor;
    private const double DentroDelRecorte = 0.03 * M;

    /// <summary>Igual altura de dos barras sacadas, al centímetro.</summary>
    private const double AlCentimetro = 0.01 * M;

    public static PreviaGenerada Generar(Document doc, ColumnaLeida columna, string carpeta, bool negros, DespieceMovible movible)
    {
        Registro.Paso("columna: previa " + columna.Elemento.Id);
        using var t = new Transaction(doc, "Vista previa de despiece de columna");
        t.Start();
        GeneradorPrevia.textosNegros = negros;
        try
        {
            TiposPropios tipos = TiposPropios.Crear(doc, false);
            View alzado = Alzado(doc, columna, tipos, movible);
            Registro.Paso("columna: exportando el alzado");
            string ruta = ExportadorImagen.Exportar(doc, alzado, carpeta);
            object? capa = null;
            if (movible.Marcas is { } marcas)
            {
                var (calibracion, _, _) = ExportadorImagen.Calibrar(ruta, marcas.A, marcas.B);
                if (calibracion is not null)
                    capa = GeneradorPrevia.Capa(movible, ConEscalaIgual(calibracion, (marcas.A.X + marcas.B.X) / 2), true);
                else
                {
                    // Mejor fija que mal alineada, como en la viga.
                    Registro.Paso("columna: sin calibración, el alzado queda fijo");
                    alzado.UnhideElements(movible.Ocultos);
                    doc.Regenerate();
                    ruta = ExportadorImagen.Exportar(doc, alzado, carpeta);
                }
            }
            // Las dos secciones superiores (decisión 16): la de traslapos solo si se supo el grupo de las varillas.
            List<Rebar> barras = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>()
                .Where(b => columna.Elementos.Contains(b.GetHostId())).ToList();
            string traslapos = "";
            if (columna.Varillas.Any(v => v.Grupo is not null))
            {
                Registro.Paso("columna: exportando la sección de traslapos");
                traslapos = ExportadorImagen.Exportar(doc, SeccionColumna.Crear(doc, columna, barras, tipos, true), carpeta);
            }
            else
                Registro.Paso("columna: sin grupos de traslapo, no va la sección de traslapos");
            Registro.Paso("columna: exportando la sección de varillas");
            string varillas = ExportadorImagen.Exportar(doc, SeccionColumna.Crear(doc, columna, barras, tipos, false), carpeta);
            return new PreviaGenerada(ruta, traslapos, capa, varillas);
        }
        finally
        {
            GeneradorPrevia.textosNegros = false;
            Registro.Paso("columna: deshaciendo");
            // La vista previa no deja nada en el modelo: los errores de Revit se descartan sin mostrarlos (decisión 33).
            t.RollBack(t.GetFailureHandlingOptions().SetClearAfterRollback(true));
        }
    }

    /// <summary>Los nombres de las vistas de un grupo: el alzado y las dos secciones (paso 6 del plan).</summary>
    public static string[] Nombres(string nombre) => [nombre, $"Sección {nombre} - traslapos", $"Sección {nombre} - varillas"];

    /// <summary>
    /// Deja las vistas de la columna en el navegador de Revit, iguales a la vista previa (paso 6 del plan): el alzado con lo
    /// movido aplicado y las dos secciones (la de traslapos solo si se supo el grupo de las varillas). No coloca nada en
    /// las hojas. Si ya existe alguna, no crea nada, como en la viga: reemplazarla podría borrar lo hecho a mano.
    /// </summary>
    public static (IReadOnlyList<string> Nombres, bool YaExistian) Crear(Document doc, ColumnaLeida columna, string nombre, bool negros, DespieceMovible movible)
    {
        string[] todos = Nombres(nombre);
        bool conTraslapos = columna.Varillas.Any(v => v.Grupo is not null);
        string[] nombres = conTraslapos ? todos : [todos[0], todos[2]];
        var existentes = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).Select(v => v.Name));
        if (nombres.Any(existentes.Contains))
            return (nombres, true);

        using var t = new Transaction(doc, "Crear vistas de despiece de columna");
        t.Start();
        GeneradorPrevia.textosNegros = negros;
        try
        {
            TiposPropios tipos = TiposPropios.Crear(doc, true);
            Alzado(doc, columna, tipos, movible).Name = todos[0];
            List<Rebar> barras = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>()
                .Where(b => columna.Elementos.Contains(b.GetHostId())).ToList();
            if (conTraslapos)
                SeccionColumna.Crear(doc, columna, barras, tipos, true).Name = todos[1];
            SeccionColumna.Crear(doc, columna, barras, tipos, false).Name = todos[2];
            if (t.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("Revit no aceptó las vistas.");
            Registro.Paso("columna: vistas creadas " + string.Join("; ", nombres));
            return (nombres, false);
        }
        finally
        {
            GeneradorPrevia.textosNegros = false;
            if (t.HasStarted() && !t.HasEnded())
                t.RollBack();
        }
    }

    /// <summary>Una de las vistas que dejó <see cref="Crear"/>: <paramref name="cual"/> es 0 (alzado), 1 (traslapos) o 2 (varillas). Null si no existe.</summary>
    public static View? VistaCreada(Document doc, string nombre, int cual)
    {
        string buscado = Nombres(nombre)[cual];
        return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => !v.IsTemplate && v.Name == buscado);
    }

    /// <summary>
    /// En la columna las cruces están lejos en altura y cerca a lo ancho: la escala buena es la vertical, y la horizontal
    /// se toma de ella (la imagen tiene la misma escala en los dos sentidos).
    /// </summary>
    private static Calibracion ConEscalaIgual(Calibracion c, double xMedia)
    {
        double escalaX = Math.Abs(c.EscalaY);
        return c with { EscalaX = escalaX, OrigenX = c.OrigenX + xMedia * (c.EscalaX - escalaX) };
    }

    /// <summary>
    /// Una barra sacada: el detalle (o las líneas, si se dibujó girada, #278), lo que ocupa (en coordenadas de la vista) y
    /// sus textos.
    /// </summary>
    private sealed class Sacada(Rebar barra, Element? detalle, List<ElementId> lineas, IList<Curve> enVista, IList<Curve> curvas, XYZ posicion, int cantidad, HashSet<ElementId> iguales)
    {
        public Rebar Barra { get; } = barra;

        /// <summary>El detalle nativo de Revit; null si la barra se dibujó girada con líneas.</summary>
        public Element? Detalle { get; } = detalle;

        /// <summary>Las líneas de detalle de la barra girada (decisión 32 del plan de #287); vacío con el detalle nativo.</summary>
        public List<ElementId> Lineas { get; } = lineas;

        /// <summary>Las curvas de esas líneas, en el plano de la vista y en <see cref="Posicion"/>.</summary>
        public IList<Curve> EnVista { get; } = enVista;

        /// <summary>Dónde va la barra girada: sus líneas se crean recién al ponerla en su lugar (decisión 37 del plan de #287).</summary>
        public XYZ Actual { get; set; } = posicion;

        /// <summary>Las curvas de la barra en el modelo, giradas si se dibujó girada: con ellas se miden ganchos y lados.</summary>
        public IList<Curve> Curvas { get; } = curvas;


        /// <summary>Donde se creó el detalle: de ahí se mueve.</summary>
        public XYZ Posicion { get; } = posicion;
        public int Cantidad { get; } = cantidad;

        /// <summary>Todas las barras que dibuja: para saber de qué varilla es pieza.</summary>
        public HashSet<ElementId> Iguales { get; } = iguales;
        public double Izquierda { get; set; }
        public double Abajo { get; set; }
        public double Ancho { get; set; }
        public double Alto { get; set; }

        /// <summary>De su borde izquierdo a la cara derecha de su tramo recto: ahí se pega la etiqueta (#276).</summary>
        public double Recto { get; set; }

        /// <summary>El diámetro de la barra: la cara izquierda del tramo recto está en <see cref="Recto"/> menos esto.</summary>
        public double Diametro { get; set; }
        public XYZ Final { get; set; } = XYZ.Zero;
        public TextNote? Etiqueta { get; set; }
        public List<TextNote> Anexos { get; } = [];
    }

    private static View Alzado(Document doc, ColumnaLeida columna, TiposPropios tipos, DespieceMovible movible)
    {
        Letra.EnAlzado();
        List<Rebar> barras = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>()
            .Where(b => columna.Elementos.Contains(b.GetHostId())).ToList();
        List<BoundingBoxXYZ> cajas = columna.Elementos.Select(id => doc.GetElement(id).get_BoundingBox(null)).Where(c => c is not null).ToList();
        var min = new XYZ(cajas.Min(c => c.Min.X), cajas.Min(c => c.Min.Y), cajas.Min(c => c.Min.Z));
        var max = new XYZ(cajas.Max(c => c.Max.X), cajas.Max(c => c.Max.Y), cajas.Max(c => c.Max.Z));
        XYZ centro = (min + max) / 2;

        // Mirando al norte, con el este a la derecha. Al crear la sección la derecha sale contraria al BasisX pedido
        // (hallazgo 9), así que se pide al revés, igual que en la viga.
        XYZ basisX = XYZ.BasisX.Negate();
        XYZ haciaElQueMira = basisX.CrossProduct(XYZ.BasisZ);
        double ancho = Math.Abs((max - min).DotProduct(XYZ.BasisX));
        double fondo = Math.Abs((max - min).DotProduct(haciaElQueMira));
        double alto = max.Z - min.Z;
        XYZ enPlano = centro + haciaElQueMira * (fondo / 2 + 0.3 * M);
        Transform t = Transform.Identity;
        t.BasisX = basisX;
        t.BasisY = XYZ.BasisZ;
        t.BasisZ = haciaElQueMira;
        t.Origin = enPlano;
        var caja = new BoundingBoxXYZ
        {
            Transform = t,
            Min = new XYZ(-ancho / 2 - 6.0 * M, -alto / 2 - 3.0 * M, -fondo - 0.7 * M),
            Max = new XYZ(ancho / 2 + 6.0 * M, alto / 2 + 3.0 * M, 0),
        };
        ViewFamilyType tipoSeccion = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(v => v.ViewFamily == ViewFamily.Section);
        ViewSection vista = GeneradorPrevia.Preparar(doc, tipoSeccion, caja, "C", Escala);
        // Lo que manda es la derecha real de la vista: Revit puede voltearla.
        XYZ derecha = vista.RightDirection;
        Registro.Paso($"columna: {barras.Count} barras; derecha {derecha}");

        Limpiar(doc, vista, columna, min, max, barras);
        GeneradorPrevia.Colorear(doc, vista, barras);
        doc.Regenerate();

        double izq = Math.Min(min.DotProduct(derecha), max.DotProduct(derecha)), der = Math.Max(min.DotProduct(derecha), max.DotProduct(derecha));
        double bordeIzq = izq - Lado, bordeDer = der + Lado;
        XYZ En(double s, double z) => new XYZ(enPlano.X, enPlano.Y, z) + derecha * (s - enPlano.DotProduct(derecha));

        // Las barras sacadas arrancan desde la cara de la columna, no después de la viga que se deja ver (decisión 13 del plan de #317).
        List<Sacada> sacadas = Sacar(doc, vista, columna, barras, derecha, der, En, tipos);
        var sueltas = new List<(Rebar Barra, string Clave, TextNote Nota)>();
        EtiquetarEstribos(doc, vista, barras, bordeIzq, En, tipos, sueltas);

        GeneradorPrevia.Recortar(vista, centro, centro.DotProduct(derecha) - bordeIzq, bordeDer - centro.DotProduct(derecha),
            centro.Z - min.Z + Holgura, max.Z - centro.Z + Holgura);
        doc.Regenerate();
        CotasCreadas cotas = Cotar(doc, vista, columna, bordeIzq, En, tipos, movible);
        vista.DisplayStyle = DisplayStyle.FlatColors;
        AplicarMovimientos(doc, vista, sacadas, sueltas, movible, derecha);
        AbrirParaGiradas(doc, vista, sacadas, min, max, derecha, centro, En, izq, der, bordeIzq, bordeDer);
        if (movible.Separar)
            Separar(doc, vista, sacadas, sueltas, movible, derecha, enPlano, bordeIzq, bordeDer, min.Z, max.Z, cotas);
        return vista;
    }

    /// <summary>
    /// Las líneas de las barras giradas las recorta el marco de la vista, y van fuera de él (decisión 38 del plan de #287).
    /// Si hay alguna, el recorte llega por la derecha hasta ellas; las vigas que así se verían largas se ocultan y su
    /// arranque (hasta <see cref="Lado"/>) se dibuja con una región gris (decisión 39). Sin barras giradas no hace nada.
    /// </summary>
    private static void AbrirParaGiradas(Document doc, View vista, List<Sacada> sacadas, XYZ min, XYZ max, XYZ derecha, XYZ centro,
        Func<double, double, XYZ> En, double izq, double der, double bordeIzq, double bordeDer)
    {
        List<Sacada> giradas = sacadas.Where(s => s.Detalle is null).ToList();
        if (giradas.Count == 0)
            return;
        double margen = 0.10 * M * Letra.Papel;
        List<BoundingBoxXYZ> cajas = giradas.Select(s => Caja(s, vista)).ToList();
        double hasta = cajas.Max(c => Math.Max(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha))) + margen;
        // Arriba y abajo también: las barras bajan a la zapata, más abajo que la pila, y sus ganchos quedaban afuera (decisión 40).
        double abajo = Math.Min(min.Z - Holgura, cajas.Min(c => c.Min.Z) - margen);
        double arriba = Math.Max(max.Z + Holgura, cajas.Max(c => c.Max.Z) + margen);

        double holgura = 0.05 * M;
        var filtro = new BoundingBoxIntersectsFilter(new Outline(min - new XYZ(holgura, holgura, holgura), max + new XYZ(holgura, holgura, holgura)));
        var ocultar = new List<ElementId>();
        foreach (Element viga in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType().WherePasses(filtro))
        {
            BoundingBoxXYZ c = viga.get_BoundingBox(null);
            if (c is null)
                continue;
            List<double> xs = new[] { c.Min, c.Max, new XYZ(c.Min.X, c.Max.Y, 0), new XYZ(c.Max.X, c.Min.Y, 0) }.Select(p => p.DotProduct(derecha)).ToList();
            double desde = xs.Min(), hastaViga = xs.Max();
            if (hastaViga <= bordeDer + 0.01 * M || !viga.CanBeHidden(vista))
                continue;
            ocultar.Add(viga.Id);
            // Lo que se veía antes: a cada lado de la columna, sin pasar por ella.
            foreach ((double a, double b) in new[] { (Math.Max(desde, bordeIzq), Math.Min(hastaViga, izq)), (Math.Max(desde, der), Math.Min(hastaViga, bordeDer)) })
            {
                if (b - a < 0.01 * M)
                    continue;
                var lazo = new CurveLoop();
                XYZ p1 = En(a, c.Min.Z), p2 = En(b, c.Min.Z), p3 = En(b, c.Max.Z), p4 = En(a, c.Max.Z);
                lazo.Append(Line.CreateBound(p1, p2));
                lazo.Append(Line.CreateBound(p2, p3));
                lazo.Append(Line.CreateBound(p3, p4));
                lazo.Append(Line.CreateBound(p4, p1));
                try
                {
                    FilledRegion.Create(doc, TipoViga(doc), vista.Id, [lazo]);
                }
                catch (Exception e)
                {
                    Registro.Paso($"columna: arranque de la viga {viga.Id} no dibujado: {e.Message}");
                }
            }
        }
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
        double centroX = centro.DotProduct(derecha);
        GeneradorPrevia.Recortar(vista, centro, centroX - bordeIzq, hasta - centroX, centro.Z - abajo, arriba - centro.Z);
        doc.Regenerate();
        Registro.Paso($"columna: recorte hasta las barras giradas ({(hasta - bordeDer) * 0.3048:0.00} m más), {ocultar.Count} vigas con el arranque dibujado");
    }

    /// <summary>El relleno gris del arranque de las vigas, como el concreto de la sección.</summary>
    private static ElementId TipoViga(Document doc)
    {
        List<FilledRegionType> tipos = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().ToList();
        if (tipos.FirstOrDefault(t => t.Name == "CQ Despiece viga") is { } propio)
            return propio.Id;
        var nuevo = (FilledRegionType)tipos.First().Duplicate("CQ Despiece viga");
        nuevo.ForegroundPatternId = GeneradorPrevia.Solido(doc);
        nuevo.ForegroundPatternColor = new Color(205, 205, 205);
        return nuevo.Id;
    }

    /// <summary>Solo la columna, sus vigas (sin su acero) y el acero de la columna (decisión 10).</summary>
    private static void Limpiar(Document doc, View vista, ColumnaLeida columna, XYZ min, XYZ max, List<Rebar> barras)
    {
        double holgura = 0.05 * M;
        var filtro = new BoundingBoxIntersectsFilter(new Outline(min - new XYZ(holgura, holgura, holgura), max + new XYZ(holgura, holgura, holgura)));
        IEnumerable<ElementId> vigas = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType().WherePasses(filtro).ToElementIds();
        var deja = new HashSet<ElementId>(barras.Select(b => b.Id).Concat(columna.Elementos).Concat(vigas));
        List<ElementId> ocultar = new FilteredElementCollector(doc, vista.Id).WhereElementIsNotElementType()
            .Where(e => e.Category is { CategoryType: CategoryType.Model } && !deja.Contains(e.Id) && e.CanBeHidden(vista))
            .Select(e => e.Id).ToList();
        if (ocultar.Count > 0)
            vista.HideElements(ocultar);
        Importados.Ocultar(doc, vista);
        foreach (BuiltInCategory c in new[] { BuiltInCategory.OST_Sections, BuiltInCategory.OST_SectionBox, BuiltInCategory.OST_Levels,
            BuiltInCategory.OST_Grids, BuiltInCategory.OST_Elev })
        {
            if (vista.CanCategoryBeHidden(new ElementId(c)))
                vista.SetCategoryHidden(new ElementId(c), true);
        }
    }

    /// <summary>
    /// Una barra de cada tipo, a la derecha y a su altura real. Las iguales en diámetro, forma, largo y altura se dibujan
    /// una vez con la cantidad sumada. Las piezas que traslapan en una misma varilla van juntas en una línea, corridas
    /// para que se vea el traslapo, con su medida a la izquierda (hallazgo 27); las líneas se reparten en carriles sin
    /// encimarse.
    /// </summary>
    private static List<Sacada> Sacar(Document doc, View vista, ColumnaLeida columna, List<Rebar> barras, XYZ derecha, double caraDer,
        Func<double, double, XYZ> En, TiposPropios tipos)
    {
        var creadas = new List<Sacada>();
        // El lado del gancho que se dibuja lo decide el grupo de traslapo: A a la izquierda, B a la derecha (decisión 18).
        Dictionary<ElementId, GrupoTraslapo> grupos = SeccionColumna.Grupos(columna);
        SentidoGancho Pedido(Rebar x) => GruposGancho.DeGrupo(grupos.TryGetValue(x.Id, out GrupoTraslapo g) ? g : null);
        SentidoGancho Real(Rebar x) => GruposGancho.DeLinea(DireccionGanchos(x, derecha));
        foreach (IGrouping<string, Rebar> iguales in barras.Where(LectorColumnas.EsVertical).GroupBy(b =>
            $"{b.GetTypeId()}|{b.GetShapeId()}|{Math.Round(LectorDespiece.LargoBarra(b) / AlCentimetro)}|{Math.Round(LectorColumnas.Alturas(b).Abajo / AlCentimetro)}"))
        {
            // La que tenga el gancho hacia el lado de su grupo y, entre esas, la más de lado: un gancho que apunta hacia
            // quien mira no se dibuja (hallazgo 25 del plan de columnas).
            SentidoGancho pedido = Pedido(iguales.First());
            Rebar b = iguales.OrderByDescending(x => pedido != SentidoGancho.Ninguno && Real(x) == pedido)
                .ThenByDescending(x => GanchosDeLado(x, derecha)).First();
            if (GanchosDeLado(b, derecha) < 0.5)
                Registro.Paso($"columna: ningún gancho de lado en las iguales a la barra {b.Id}");
            if (pedido != SentidoGancho.Ninguno && Real(b) != pedido)
                Registro.Paso($"columna: ninguna de las iguales a la barra {b.Id} tiene el gancho a la {pedido} (su grupo); va como está");
            (double abajo, double arriba) = LectorColumnas.Alturas(b);
            try
            {
                XYZ p = En(caraDer + 3 * M, (abajo + arriba) / 2);
                int cantidad = iguales.Sum(x => x.Quantity);
                HashSet<ElementId> ids = iguales.Select(x => x.Id).ToHashSet();
                bool aLaIzquierda = pedido == SentidoGancho.Izquierda;
                if (GanchosDeLado(b, derecha) < 0.5 && Girar(b, aLaIzquierda ? derecha.Negate() : derecha) is { } giro)
                {
                    // El gancho apunta hacia quien mira: se dibuja girada, como vista desde la otra cara (decisión 32).
                    // Sin crear nada todavía: las líneas van directo a su carril, en Colocar (decisión 37).
                    creadas.Add(new Sacada(b, null, [], GiradaEnVista(vista, giro, p), giro, p, cantidad, ids));
                    Registro.Paso($"columna: barra {b.Id} se dibuja girada, gancho a la {(aLaIzquierda ? "izquierda" : "derecha")}");
                }
                else
                {
                    Element d = RebarBendingDetail.Create(doc, vista.Id, b.Id, 0, tipos.BarraSinMedidas, p, 0);
                    GeneradorPrevia.Pintar(vista, d.Id, b, 2);
                    creadas.Add(new Sacada(b, d, [], [], GeneradorPrevia.Curvas(b, false), p, cantidad, ids));
                }
            }
            catch (Exception e)
            {
                // Revit solo dibuja el detalle de una barra que se ve en la vista (lo mismo pasó con estribos en la viga).
                Registro.Paso($"columna: sin detalle para la barra {b.Id}: {e.Message}");
            }
        }
        doc.Regenerate();

        foreach (Sacada s in creadas)
        {
            BoundingBoxXYZ m = Caja(s, vista);
            double a = m.Min.DotProduct(derecha), e = m.Max.DotProduct(derecha);
            s.Izquierda = Math.Min(a, e);
            s.Ancho = Math.Abs(e - a);
            s.Alto = m.Max.Z - m.Min.Z;
            // A su altura real: el borde de abajo del detalle donde está el de la barra.
            double d = ((RebarBarType)doc.GetElement(s.Barra.GetTypeId())).BarNominalDiameter;
            s.Abajo = LectorColumnas.Alturas(s.Barra).Abajo - d / 2;
            s.Final = s.Posicion + XYZ.BasisZ * (s.Abajo - m.Min.Z);
            // El tramo recto, no toda la caja: con el gancho a la derecha la etiqueta quedaba lejos de la barra (#276).
            double izquierdaEje = s.Curvas.SelectMany(c => c.Tessellate()).Min(p => p.DotProduct(derecha));
            double recto = GeneradorPrevia.Mayor(s.Barra).GetEndPoint(0).DotProduct(derecha) - izquierdaEje + d / 2;
            s.Recto = Math.Clamp(recto, 0, s.Ancho);
            s.Diametro = d;
            Registro.Paso($"columna: barra {s.Barra.Id} ancho {s.Ancho * 0.3048:0.000} recto a {s.Recto * 0.3048:0.000}");
        }

        List<List<Sacada>> lineas = Lineas(columna, creadas);
        // Cada línea: a la izquierda la medida del traslapo (si tiene), luego las piezas corridas y a la derecha sus etiquetas.
        double Izquierdo(List<Sacada> l) => l.Count > 1 ? AnchoNotaGirada + AireNota : 0;
        double Desfase(int j) => j % 2 * DesfaseTraslapo;
        PiezaFila Pieza(int i) => new(i, lineas[i].Min(s => s.Abajo) - AltoTexto, lineas[i].Max(s => s.Abajo + s.Alto) + AltoTexto,
            Izquierdo(lineas[i]) + lineas[i].Select((s, j) => Desfase(j) + Math.Max(s.Ancho, s.Recto + AireNota + AnchoNotaGirada)).Max());
        // Primero las de gancho a la derecha, juntas; un hueco; después las de la izquierda: en espejo (#277).
        // Por el gancho que de verdad se dibujó: ya se eligió, de las iguales, la que mira hacia el lado de su grupo si había
        // alguna; si no había, no se fuerza (Luciano, 06-nov) y la línea va con el lado que tiene.
        List<SentidoGancho> sentidos = lineas.Select(l => GruposGancho.DeLinea(l.SelectMany(s => DireccionGanchos(s.Barra, s.Curvas, derecha)))).ToList();
        Registro.Paso("columna: ganchos por línea " + string.Join(", ", sentidos.Select((x, i) => $"{i}:{x}")));
        // Las vigas que asoman a la derecha (hasta Lado de la cara): las barras no van encima de ellas, sino justo después,
        // y solo a su altura; donde no hay viga siguen pegadas a la columna (punto de Luciano del 10-11, paso 8 del plan).
        List<Rectangulo> vigas = VigasALaDerecha(doc, columna, derecha, caraDer)
            .Select(v => new Rectangulo(v.Abajo, v.Arriba, 0, Lado + InicioSacadas)).ToList();
        Registro.Paso("columna: vigas a la derecha " + string.Join(", ", vigas.Select(v => $"{v.Desde * 0.3048:0.00} a {v.Hasta * 0.3048:0.00}")));
        // Derecha, sin gancho de lado e izquierda, cada grupo junto y con un hueco entre grupos (decisión 24). En cada grupo,
        // cada línea lo más cerca de la columna sin tocar a las otras ni a las vigas (Empacador): una corta puede entrar en
        // el hueco que dejan las otras.
        var ubicadas = new List<(int Indice, double Inicio)>();
        double inicioGrupo = InicioSacadas;
        foreach (IReadOnlyList<int> grupo in GruposGancho.Separar(sentidos).Where(g => g.Count > 0))
        {
            List<int> orden = grupo.OrderBy(i => lineas[i].Min(s => s.Abajo)).ToList();
            List<GrupoEmpacado> empacados = orden.Select(i =>
            {
                PiezaFila p = Pieza(i);
                return new GrupoEmpacado([new Rectangulo(p.Izquierda, p.Derecha, 0, p.Alto)], inicioGrupo);
            }).ToList();
            IReadOnlyList<double> inicios = Empacador.Colocar(empacados, inicioGrupo, HuecoEnCarril, HuecoCarriles, obstaculos: vigas);
            for (int k = 0; k < orden.Count; k++)
                ubicadas.Add((orden[k], caraDer + inicios[k]));
            inicioGrupo = orden.Select((i, k) => inicios[k] + Pieza(i).Alto).Max() + HuecoGrupos;
        }
        foreach ((int indice, double piso) in ubicadas)
        {
            List<Sacada> linea = lineas[indice];
            double inicio = piso + Izquierdo(linea);
            for (int j = 0; j < linea.Count; j++)
            {
                Sacada s = linea[j];
                double x = inicio + Desfase(j);
                s.Final += derecha * (x - s.Izquierda);
                s.Izquierda = x;
                Colocar(doc, vista, s, s.Final);

                string tipo = doc.GetElement(s.Barra.GetTypeId()).Name;
                string texto = LectorDespiece.Etiqueta(tipo, s.Cantidad, LectorDespiece.LargoBarra(s.Barra), GeneradorPrevia.Curvas(s.Barra, true).Sum(c => c.Length));
                // Girada, la caja de la nota crece hacia la derecha de su punto (como antes, cuando iba tras la caja entera).
                s.Etiqueta = TextNote.Create(doc, vista.Id, En(s.Izquierda + s.Recto + AireNota, s.Abajo + s.Alto / 2), texto, Girada(tipos));
                GeneradorPrevia.Pintar(vista, s.Etiqueta.Id, s.Barra, null);
                MedirGanchos(doc, vista, s, derecha, En, tipos);
                if (j > 0)
                    MedirTraslapo(doc, vista, linea[j - 1], s, inicio, En, tipos);
            }
        }
        doc.Regenerate();
        // Cuánto aire queda de verdad entre la cara derecha del tramo recto y la caja de su etiqueta (paso 9 del plan del
        // 10-11: "textos más cerca de las barras"), para corregir con lo medido y no a ojo.
        foreach (Sacada s in creadas.Where(x => x.Etiqueta is not null))
        {
            if (s.Etiqueta!.get_BoundingBox(vista) is not { } c)
                continue;
            double cajaIzq = Math.Min(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha)), cajaDer = Math.Max(c.Min.DotProduct(derecha), c.Max.DotProduct(derecha));
            Registro.Paso($"columna: etiqueta de {s.Barra.Id}: aire {(cajaIzq - (s.Izquierda + s.Recto)) * 0.3048:0.000} m, caja de {(cajaDer - cajaIzq) * 0.3048:0.000} m"
                + $" (AireNota {AireNota * 0.3048:0.000}, AnchoNotaGirada {AnchoNotaGirada * 0.3048:0.000})");
        }
        return creadas;
    }

    /// <summary>
    /// Las vigas que tocan la columna y salen hacia su derecha en la vista: entre qué alturas están. Son las que se dejan
    /// ver hasta <see cref="Lado"/> de la cara (el mismo filtro que <see cref="Limpiar"/>).
    /// </summary>
    private static List<(double Abajo, double Arriba)> VigasALaDerecha(Document doc, ColumnaLeida columna, XYZ derecha, double caraDer)
    {
        List<BoundingBoxXYZ> cajas = columna.Elementos.Select(id => doc.GetElement(id).get_BoundingBox(null)).Where(c => c is not null).ToList();
        if (cajas.Count == 0)
            return [];
        double holgura = 0.05 * M;
        var min = new XYZ(cajas.Min(c => c.Min.X) - holgura, cajas.Min(c => c.Min.Y) - holgura, cajas.Min(c => c.Min.Z) - holgura);
        var max = new XYZ(cajas.Max(c => c.Max.X) + holgura, cajas.Max(c => c.Max.Y) + holgura, cajas.Max(c => c.Max.Z) + holgura);
        return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType()
            .WherePasses(new BoundingBoxIntersectsFilter(new Outline(min, max)))
            .Select(v => v.get_BoundingBox(null)).Where(c => c is not null)
            .Where(c => new[] { c.Min, c.Max, new XYZ(c.Min.X, c.Max.Y, 0), new XYZ(c.Max.X, c.Min.Y, 0) }.Max(p => p.DotProduct(derecha)) > caraDer + 0.01 * M)
            .Select(c => (c.Min.Z, c.Max.Z)).ToList();
    }

    /// <summary>
    /// Las curvas de la barra giradas alrededor de la vertical de su tramo recto, hasta que la pata de su primer gancho
    /// vaya hacia <paramref name="hacia"/> (decisión 32 del plan de #287): como si se la viera desde la otra cara. Null si
    /// no tiene gancho o su pata no tiene parte horizontal.
    /// </summary>
    private static IList<Curve>? Girar(Rebar b, XYZ hacia)
    {
        IList<Curve> curvas = GeneradorPrevia.Curvas(b, false);
        if (curvas.Count < 3)
            return null;
        XYZ? pata = b.GetHookTypeId(0) != ElementId.InvalidElementId ? curvas[0].GetEndPoint(0) - curvas[0].GetEndPoint(1)
            : b.GetHookTypeId(1) != ElementId.InvalidElementId ? curvas[^1].GetEndPoint(1) - curvas[^1].GetEndPoint(0) : null;
        if (pata is null)
            return null;
        var plana = new XYZ(pata.X, pata.Y, 0);
        if (plana.GetLength() < 1e-6)
            return null;
        plana = plana.Normalize();
        XYZ destino = new XYZ(hacia.X, hacia.Y, 0).Normalize();
        double angulo = Math.Atan2(plana.CrossProduct(destino).Z, plana.DotProduct(destino));
        Transform giro = Transform.CreateRotationAtPoint(XYZ.BasisZ, angulo, GeneradorPrevia.Mayor(b).GetEndPoint(0));
        return curvas.Select(c => c.CreateTransformed(giro)).ToList();
    }

    /// <summary>
    /// Las curvas giradas llevadas al plano de la vista (quedan paralelas a ella) y centradas en <paramref name="punto"/>,
    /// como el detalle nativo.
    /// </summary>
    private static IList<Curve> GiradaEnVista(View vista, IList<Curve> curvas, XYZ punto)
    {
        XYZ hacia = vista.ViewDirection;
        List<XYZ> puntos = curvas.SelectMany(c => c.Tessellate()).ToList();
        XYZ alPlano = hacia * (vista.Origin - puntos[0]).DotProduct(hacia);
        var min = new XYZ(puntos.Min(p => p.X), puntos.Min(p => p.Y), puntos.Min(p => p.Z));
        var max = new XYZ(puntos.Max(p => p.X), puntos.Max(p => p.Y), puntos.Max(p => p.Z));
        Transform llevar = Transform.CreateTranslation(punto - ((min + max) / 2 + alPlano));
        return curvas.Select(c => c.CreateTransformed(Transform.CreateTranslation(alPlano)).CreateTransformed(llevar)).ToList();
    }

    /// <summary>Las líneas de detalle de esas curvas, en el color de la barra.</summary>
    private static List<ElementId> Trazar(Document doc, View vista, Rebar b, IEnumerable<Curve> curvas)
    {
        var lineas = new List<ElementId>();
        foreach (Curve c in curvas)
        {
            DetailCurve linea = doc.Create.NewDetailCurve(vista, c);
            GeneradorPrevia.Pintar(vista, linea.Id, b, 2);
            lineas.Add(linea.Id);
        }
        return lineas;
    }

    /// <summary>Lo que dibuja la barra en la vista: el detalle o sus líneas.</summary>
    private static IEnumerable<ElementId> Dibujo(Sacada s) => s.Detalle is { } d ? [d.Id] : s.Lineas;

    /// <summary>Lo que ocupa: la del detalle, o la de las curvas de la barra girada donde está hoy (sin depender de sus líneas).</summary>
    private static BoundingBoxXYZ Caja(Sacada s, View vista)
    {
        if (s.Detalle is { } d)
            return d.get_BoundingBox(vista);
        XYZ corrida = s.Actual - s.Posicion;
        List<XYZ> puntos = s.EnVista.SelectMany(c => c.Tessellate()).Select(p => p + corrida).ToList();
        return new BoundingBoxXYZ
        {
            Min = new XYZ(puntos.Min(p => p.X), puntos.Min(p => p.Y), puntos.Min(p => p.Z)),
            Max = new XYZ(puntos.Max(p => p.X), puntos.Max(p => p.Y), puntos.Max(p => p.Z)),
        };
    }

    /// <summary>
    /// Pone la barra en <paramref name="posicion"/>: el detalle con su API; las líneas se crean ahí (y si ya estaban en
    /// otro lado, se borran antes). Moverlas hacía que Revit avisara "Can't keep elements joined" (decisión 33 del plan
    /// de #287), y crearlas todas en el mismo punto antes de llevarlas a su carril las mezclaba (decisión 37).
    /// </summary>
    private static void Colocar(Document doc, View vista, Sacada s, XYZ posicion)
    {
        if (s.Detalle is { } d)
        {
            RebarBendingDetail.SetPosition(d, posicion);
            return;
        }
        if (s.Lineas.Count > 0)
            doc.Delete(s.Lineas);
        Transform llevar = Transform.CreateTranslation(posicion - s.Posicion);
        List<ElementId> nuevas = Trazar(doc, vista, s.Barra, s.EnVista.Select(c => c.CreateTransformed(llevar)));
        s.Lineas.Clear();
        s.Lineas.AddRange(nuevas);
        s.Actual = posicion;
    }

    /// <summary>
    /// Agrupa las barras sacadas en líneas: la pieza que sigue a otra en una misma varilla (de abajo hacia arriba) va en la
    /// misma línea. Sale de <see cref="ColumnaLeida.Varillas"/>; una sacada sin varilla conocida va sola.
    /// </summary>
    private static List<List<Sacada>> Lineas(ColumnaLeida columna, List<Sacada> sacadas)
    {
        int Indice(ElementId barra) => sacadas.FindIndex(s => s.Iguales.Contains(barra));
        var siguiente = new Dictionary<int, int>();
        foreach (Varilla v in columna.Varillas)
        {
            for (int k = 0; k + 1 < v.Piezas.Count; k++)
            {
                int a = Indice(v.Piezas[k].Barra), b = Indice(v.Piezas[k + 1].Barra);
                if (a >= 0 && b >= 0 && a != b && !siguiente.ContainsKey(a) && !siguiente.ContainsValue(b))
                    siguiente[a] = b;
            }
        }
        foreach (Varilla v in columna.Varillas)
            Registro.Paso($"columna: varilla ({v.Medida.X:0.000}, {v.Medida.Y:0.000}) {v.Grupo} piezas "
                + string.Join(", ", v.Piezas.Select(p => $"{p.Barra}/{p.Indice} [{p.ZInferior * 0.3048:0.00} a {p.ZSuperior * 0.3048:0.00}] sacada {Indice(p.Barra)}")));
        var usadas = new HashSet<int>();
        var lineas = new List<List<Sacada>>();
        foreach (int i in Enumerable.Range(0, sacadas.Count).Where(i => !siguiente.ContainsValue(i)).OrderBy(i => sacadas[i].Abajo))
        {
            var linea = new List<Sacada>();
            int? k = i;
            while (k is { } actual && usadas.Add(actual))
            {
                linea.Add(sacadas[actual]);
                k = siguiente.TryGetValue(actual, out int n) ? n : null;
            }
            lineas.Add(linea);
        }
        // Lo que quedó en un ciclo (no debería pasar): cada una sola.
        lineas.AddRange(Enumerable.Range(0, sacadas.Count).Where(usadas.Add).Select(i => new List<Sacada> { sacadas[i] }));
        foreach (List<Sacada> l in lineas)
            Registro.Paso("columna: línea " + string.Join(" + ", l.Select(s => $"{s.Barra.Id} ({s.Cantidad}, {s.Abajo * 0.3048:0.00} a {(s.Abajo + s.Alto) * 0.3048:0.00})")));
        return lineas;
    }

    /// <summary>La medida del traslapo entre dos piezas seguidas, de sus tramos rectos, girada a la izquierda de la línea.</summary>
    private static void MedirTraslapo(Document doc, View vista, Sacada abajo, Sacada arriba, double inicio, Func<double, double, XYZ> En, TiposPropios tipos)
    {
        static (double Desde, double Hasta) Tramo(Rebar b)
        {
            Curve c = GeneradorPrevia.Mayor(b);
            return (Math.Min(c.GetEndPoint(0).Z, c.GetEndPoint(1).Z), Math.Max(c.GetEndPoint(0).Z, c.GetEndPoint(1).Z));
        }
        (double a0, double a1) = Tramo(abajo.Barra);
        (double b0, double b1) = Tramo(arriba.Barra);
        double desde = Math.Max(a0, b0), hasta = Math.Min(a1, b1);
        if (hasta - desde < AlCentimetro)
            return;
        // Pegada al tramo recto que queda más a la izquierda de las dos piezas, no al borde de la línea, que incluye los
        // ganchos (decisión 21 del plan de #287). Girada, la caja de la nota crece hacia la derecha de su punto.
        double recto = Math.Min(abajo.Izquierda + abajo.Recto - abajo.Diametro, arriba.Izquierda + arriba.Recto - arriba.Diametro);
        TextNote nota = TextNote.Create(doc, vista.Id, En(recto - AireNota - AnchoNotaGirada, (desde + hasta) / 2),
            ((hasta - desde) * 0.3048).ToString("0.00", CultureInfo.InvariantCulture), Girada(tipos));
        GeneradorPrevia.Pintar(vista, nota.Id, arriba.Barra, null);
        arriba.Anexos.Add(nota);
    }

    private static TextNoteOptions Girada(TiposPropios tipos)
    {
        TextNoteOptions girada = tipos.Nota(HorizontalTextAlignment.Center);
        girada.Rotation = Math.PI / 2;
        return girada;
    }

    /// <summary>
    /// Hacia dónde va la punta de cada gancho en la vista, de -1 (a la izquierda) a 1 (a la derecha): de la pata, desde
    /// el doblez hacia la punta. La primera curva va de la punta al doblez; la última, del doblez a la punta.
    /// </summary>
    private static IEnumerable<double> DireccionGanchos(Rebar b, XYZ derecha) => DireccionGanchos(b, GeneradorPrevia.Curvas(b, false), derecha);

    /// <summary>Lo mismo con las curvas que se dibujaron (giradas, si la barra se dibujó girada).</summary>
    private static IEnumerable<double> DireccionGanchos(Rebar b, IList<Curve> curvas, XYZ derecha)
    {
        if (curvas.Count < 3)
            yield break;
        if (b.GetHookTypeId(0) != ElementId.InvalidElementId)
            yield return (curvas[0].GetEndPoint(0) - curvas[0].GetEndPoint(1)).Normalize().DotProduct(derecha);
        if (b.GetHookTypeId(1) != ElementId.InvalidElementId)
            yield return (curvas[^1].GetEndPoint(1) - curvas[^1].GetEndPoint(0)).Normalize().DotProduct(derecha);
    }

    /// <summary>
    /// Qué tan de lado quedan sus ganchos en la vista: 1 si todos van a lo largo de <paramref name="derecha"/>, 0 si alguno
    /// apunta hacia quien mira. Sin ganchos, 1.
    /// </summary>
    private static double GanchosDeLado(Rebar b, XYZ derecha)
    {
        IList<Curve> curvas = GeneradorPrevia.Curvas(b, false);
        if (curvas.Count < 3)
            return 1;
        var patas = new List<Curve>();
        if (b.GetHookTypeId(0) != ElementId.InvalidElementId)
            patas.Add(curvas[0]);
        if (b.GetHookTypeId(1) != ElementId.InvalidElementId)
            patas.Add(curvas[^1]);
        return patas.Select(p => Math.Abs((p.GetEndPoint(1) - p.GetEndPoint(0)).Normalize().DotProduct(derecha))).DefaultIfEmpty(1).Min();
    }

    /// <summary>
    /// El largo de cada gancho, en texto bajo la pata (o encima, si el gancho va arriba), como en la viga: el detalle no
    /// muestra la medida del gancho sin la del tramo, y esa ya va en la etiqueta.
    /// </summary>
    private static void MedirGanchos(Document doc, View vista, Sacada s, XYZ derecha, Func<double, double, XYZ> En, TiposPropios tipos)
    {
        IList<Curve> curvas = s.Curvas;
        if (curvas.Count < 3)
            return;
        // El detalle es la barra trasladada: de un punto de la barra a la vista.
        List<XYZ> puntos = curvas.SelectMany(c => c.Tessellate()).ToList();
        double dx = s.Izquierda - puntos.Min(p => p.DotProduct(derecha)), dz = s.Abajo - puntos.Min(p => p.Z);
        double medio = (puntos.Min(p => p.Z) + puntos.Max(p => p.Z)) / 2;
        double d = ((RebarBarType)doc.GetElement(s.Barra.GetTypeId())).BarNominalDiameter;

        void Medir(Curve pata, Curve doblez)
        {
            double largo = pata.Length + (doblez is Arc arco ? arco.Radius : 0) + d / 2;
            XYZ c = (pata.GetEndPoint(0) + pata.GetEndPoint(1)) / 2;
            double x = c.DotProduct(derecha) + dx, z = c.Z + dz;
            bool abajo = c.Z < medio;
            TextNote nota = TextNote.Create(doc, vista.Id, En(x, abajo ? z - AireNota : z + BajaNota + AireNota),
                (largo * 0.3048).ToString("0.00", CultureInfo.InvariantCulture), tipos.Nota(HorizontalTextAlignment.Center));
            GeneradorPrevia.Pintar(vista, nota.Id, s.Barra, null);
            s.Anexos.Add(nota);
        }

        if (s.Barra.GetHookTypeId(0) != ElementId.InvalidElementId)
            Medir(curvas[0], curvas[1]);
        if (s.Barra.GetHookTypeId(1) != ElementId.InvalidElementId)
            Medir(curvas[^1], curvas[^2]);
    }

    /// <summary>
    /// Una etiqueta por zona de estribos, a la izquierda, a la altura del centro de su zona; si dos quedan muy cerca, sube
    /// la de arriba (<see cref="EtiquetasEstribos.SinEncimar"/>). Las zonas salen de la altura de cada estribo, por
    /// diámetro (<see cref="ZonasEstribos.Agrupar"/>): sirve igual con conjuntos que con estribos sueltos (hallazgo 29).
    /// Estribo y grapa a la misma altura cuentan una vez.
    /// </summary>
    private static void EtiquetarEstribos(Document doc, View vista, List<Rebar> barras, double bordeIzq, Func<double, double, XYZ> En, TiposPropios tipos, List<(Rebar Barra, string Clave, TextNote Nota)> sueltas)
    {
        var escritas = new List<(Rebar Barra, string Clave, string Texto, double Centro)>();
        // Por forma y largo, no solo por diámetro: a cada altura puede haber estribo y grapa a pocos centímetros, y juntos
        // nunca dan una separación constante (hallazgo 33).
        var porFormas = new List<(IGrouping<string, Rebar> Forma, List<(Rebar Barra, int Indice, double Z)> Estribos)>();
        foreach (IGrouping<string, Rebar> porForma in barras.Where(b => !LectorColumnas.EsVertical(b))
            .GroupBy(b => $"{b.GetTypeId()}|{b.GetShapeId()}|{Math.Round(LectorDespiece.LargoBarra(b) / AlCentimetro)}"))
        {
            var estribos = new List<(Rebar Barra, int Indice, double Z)>();
            foreach (Rebar b in porForma)
            {
                for (int i = 0; i < b.NumberOfBarPositions; i++)
                {
                    // Revit no deja leer IncludeFirstBar/IncludeLastBar de una barra suelta (regla Single): lanza (04-nov).
                    bool varias = b.LayoutRule != RebarLayoutRule.Single;
                    if (varias && ((i == 0 && !b.IncludeFirstBar) || (i == b.NumberOfBarPositions - 1 && i > 0 && !b.IncludeLastBar)))
                        continue;
                    List<XYZ> puntos = b.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, i)
                        .SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) }).ToList();
                    if (puntos.Count > 0)
                        estribos.Add((b, i, puntos.Average(p => p.Z)));
                }
            }
            if (estribos.Count > 0)
                porFormas.Add((porForma, estribos));
        }
        // Sin los textos de los estribos menores (#316, Santiago 09-nov): las grapas y cajas se siguen dibujando, pero a la
        // izquierda solo van los principales.
        var principales = new HashSet<string>(EstribosPrincipales.Filtrar(porFormas.Select(f => new FormaEstribo(f.Forma.Key,
            LectorDespiece.LargoBarra(f.Forma.First()) * 0.3048, f.Estribos.Min(e => e.Z) * 0.3048, f.Estribos.Max(e => e.Z) * 0.3048)).ToList()));
        Registro.Paso($"columna: estribos principales {principales.Count} de {porFormas.Count} formas");
        foreach ((IGrouping<string, Rebar> porForma, List<(Rebar Barra, int Indice, double Z)> estribos) in porFormas.Where(f => principales.Contains(f.Forma.Key)))
        {
            string tipo = doc.GetElement(porForma.First().GetTypeId()).Name;
            foreach (ZonaEstribos zona in ZonasEstribos.Agrupar(estribos.Select(e => e.Z * 0.3048).ToList()))
            {
                (Rebar primera, int indice, _) = estribos[zona.Indices.Min()];
                string texto = LectorDespiece.EtiquetaEstribos(tipo, zona.Cantidad, zona.Separacion / 0.3048);
                // La grapa con el mismo texto en la misma zona se escribe una vez, como en la viga.
                if (!escritas.Any(e => e.Texto == texto && Math.Abs(e.Centro - zona.Centro / 0.3048) < MismaZona))
                    escritas.Add((primera, $"e{primera.Id.Value}-{indice}", texto, zona.Centro / 0.3048));
            }
        }
        escritas = escritas.OrderBy(e => e.Centro).ToList();
        Registro.Paso("columna: zonas de estribos " + string.Join("; ", escritas.Select(e => $"{e.Texto} en {e.Centro * 0.3048:0.00}")));
        IReadOnlyList<double> alturas = EtiquetasEstribos.SinEncimar(escritas.Select(e => e.Centro).ToList(), FilaEstribos);
        for (int i = 0; i < escritas.Count; i++)
        {
            // La caja de la nota baja desde su punto: con media caja más arriba, queda centrada en su zona.
            TextNote nota = TextNote.Create(doc, vista.Id, En(bordeIzq - AireEstribos, alturas[i] + BajaNota / 2), escritas[i].Texto,
                tipos.Nota(HorizontalTextAlignment.Right));
            GeneradorPrevia.Pintar(vista, nota.Id, escritas[i].Barra, null);
            sueltas.Add((escritas[i].Barra, escritas[i].Clave, nota));
        }
    }

    /// <summary>
    /// A la izquierda: la cadena de los elementos de la pila (dado, piso 1, piso 2…) y, más afuera, el alto total. Contra
    /// planos de referencia en blanco, como las cotas de columnas de la viga. Si Revit no las acepta, la vista sale sin ellas.
    /// </summary>
    private static CotasCreadas Cotar(Document doc, View vista, ColumnaLeida columna, double bordeIzq, Func<double, double, XYZ> En, TiposPropios tipos, DespieceMovible movible)
    {
        // Abajo: la cadena de elementos; Arriba: el alto total (los nombres de CotasCreadas son los de la viga).
        var cotas = new CotasCreadas();
        // Cada una donde el usuario la dejó en el paso 2 (paso 10 del plan del 10-11).
        double xCadena = bordeIzq - CotaElementos + GeneradorPrevia.Corrimiento(movible, ClaveCotaElementos, true);
        double xTotal = bordeIzq - CotaTotal + GeneradorPrevia.Corrimiento(movible, ClaveCotaTotal, true);
        var marcas = new List<double>();
        foreach (BoundingBoxXYZ c in columna.Elementos.Select(id => doc.GetElement(id).get_BoundingBox(null)).Where(c => c is not null))
            foreach (double z in new[] { c.Min.Z, c.Max.Z })
                if (!marcas.Any(m => Math.Abs(m - z) < AlCentimetro))
                    marcas.Add(z);
        marcas.Sort();
        if (marcas.Count < 2)
            return cotas;

        using var prueba = new SubTransaction(doc);
        prueba.Start();
        try
        {
            var blanco = new OverrideGraphicSettings();
            blanco.SetProjectionLineColor(new Color(255, 255, 255));
            List<Reference> planos = marcas.Select(z =>
            {
                ReferencePlane plano = doc.Create.NewReferencePlane(En(bordeIzq + DentroDelRecorte, z), En(Math.Min(xCadena, xTotal) - DentroDelRecorte, z), vista.ViewDirection, vista);
                vista.SetElementOverrides(plano.Id, blanco);
                return plano.GetReference();
            }).ToList();
            doc.Regenerate();

            Dimension Cota(IEnumerable<Reference> referencias, double x)
            {
                var lista = new ReferenceArray();
                foreach (Reference r in referencias)
                    lista.Append(r);
                Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(En(x, marcas[0]), En(x, marcas[^1])), lista);
                if (tipos.CotaCorta is { } tipo)
                    cota.DimensionType = tipo;
                return cota;
            }
            // Con un solo elemento la cadena y el total dirían lo mismo.
            Dimension? cadena = marcas.Count > 2 ? Cota(planos, xCadena) : null;
            Dimension total = Cota([planos[0], planos[^1]], xTotal);
            prueba.Commit();
            if (cadena is not null)
                cotas.Abajo.Add(cadena);
            cotas.Arriba.Add(total);
        }
        catch (Exception e)
        {
            Registro.Paso("columna: cotas no creadas: " + e.Message);
            prueba.RollBack();
        }
        return cotas;
    }

    /// <summary>Las claves de las dos cotas de la izquierda, que se arrastran a los lados (paso 10 del plan del 10-11).</summary>
    private const string ClaveCotaElementos = "cota-elementos", ClaveCotaTotal = "cota-total";

    /// <summary>Lo que el usuario movió en el paso 2: la barra a los lados (con su etiqueta y sus ganchos) y los textos libres.</summary>
    private static void AplicarMovimientos(Document doc, View vista, List<Sacada> sacadas, List<(Rebar Barra, string Clave, TextNote Nota)> sueltas, DespieceMovible movible, XYZ derecha)
    {
        foreach (Sacada s in sacadas)
        {
            if (!movible.Movimientos.TryGetValue(s.Barra.Id.Value.ToString(), out Movimiento? m))
                continue;
            XYZ barra = derecha * (m.Dx / 0.3048) + XYZ.BasisZ * (m.Dz / 0.3048);
            Colocar(doc, vista, s, s.Final + barra);
            foreach (TextNote anexo in s.Anexos)
                anexo.Coord += barra;
            if (s.Etiqueta is not null)
                s.Etiqueta.Coord += barra + derecha * (m.Tx / 0.3048) + XYZ.BasisZ * (m.Tz / 0.3048);
        }
        foreach ((_, string clave, TextNote nota) in sueltas)
        {
            if (movible.Movimientos.TryGetValue(clave, out Movimiento? m))
                nota.Coord += derecha * (m.Tx / 0.3048) + XYZ.BasisZ * (m.Tz / 0.3048);
        }
        doc.Regenerate();
    }

    /// <summary>Como en la viga: lo movible se mide, se oculta de la imagen y se entrega para que la página lo dibuje encima.</summary>
    private static void Separar(Document doc, View vista, List<Sacada> sacadas, List<(Rebar Barra, string Clave, TextNote Nota)> sueltas, DespieceMovible movible,
        XYZ derecha, XYZ enPlano, double bordeIzq, double bordeDer, double zInf, double zSup, CotasCreadas? cotas = null)
    {
        (double A, double B) EnX(BoundingBoxXYZ c)
        {
            double a = c.Min.DotProduct(derecha), b = c.Max.DotProduct(derecha);
            return (Math.Min(a, b), Math.Max(a, b));
        }
        TextoMovible Texto(TextNote nota)
        {
            bool girada = Math.Abs(nota.BaseDirection.Z) > 0.5;
            // Girada, la caja que da Revit es mucho más ancha que el texto (0.40 m con letra de 2.5 mm a 1:50, registro del
            // 10-11) y su centro dejaba la etiqueta lejos de la barra en la página. Se usa su punto: el texto centrado en él a
            // lo alto y, a lo ancho, la caja crece hacia la derecha del punto (paso 9 del plan del 10-11).
            if (girada)
                return new TextoMovible(nota.Text.Trim(), new PuntoModelo(nota.Coord.DotProduct(derecha) + AnchoNotaGirada / 2, nota.Coord.Z), true);
            BoundingBoxXYZ c = nota.get_BoundingBox(vista);
            (double a, double b) = EnX(c);
            return new TextoMovible(nota.Text.Trim(), new PuntoModelo((a + b) / 2, (c.Min.Z + c.Max.Z) / 2), false);
        }
        void Ocultar(Element e)
        {
            if (e.CanBeHidden(vista))
                movible.Ocultos.Add(e.Id);
        }

        foreach (Sacada s in sacadas.Where(x => x.Etiqueta is not null))
        {
            BoundingBoxXYZ caja = Caja(s, vista);
            (double izquierda, _) = EnX(caja);
            List<XYZ> curva = s.Curvas.SelectMany(c => c.Tessellate()).ToList();
            double dx = izquierda - curva.Min(p => p.DotProduct(derecha)), dz = caja.Min.Z - curva.Min(p => p.Z);
            List<PuntoModelo> puntos = curva.Select(p => new PuntoModelo(p.DotProduct(derecha) + dx, p.Z + dz)).ToList();
            movible.Piezas.Add(new PiezaMovible(s.Barra.Id.Value.ToString(), LectorDespiece.Color(doc.GetElement(s.Barra.GetTypeId()).Name), puntos,
                Texto(s.Etiqueta!), s.Anexos.Select(Texto).ToList()));
            foreach (ElementId id in Dibujo(s))
                Ocultar(doc.GetElement(id));
            Ocultar(s.Etiqueta!);
            s.Anexos.ForEach(Ocultar);
        }
        foreach ((Rebar barra, string clave, TextNote nota) in sueltas)
        {
            movible.Piezas.Add(new PiezaMovible(clave, LectorDespiece.Color(doc.GetElement(barra.GetTypeId()).Name), [], Texto(nota), []));
            Ocultar(nota);
        }
        // Las dos cotas de la izquierda, cada una entera; sus líneas de referencia van hacia la columna.
        if (cotas is not null)
        {
            foreach ((string id, List<Element> elementos) in new[] { (ClaveCotaElementos, cotas.Abajo), (ClaveCotaTotal, cotas.Arriba) })
            {
                var cota = new CotaMovible(id, [], [], []);
                foreach (Dimension d in elementos.OfType<Dimension>())
                {
                    GeneradorPrevia.MedirCota(d, cota, derecha, XYZ.BasisZ, derecha * GeneradorPrevia.LineaReferencia, true);
                    Ocultar(d);
                }
                if (cota.Lineas.Count > 0)
                    movible.Cotas.Add(cota);
            }
        }
        if (movible.Ocultos.Count > 0)
            vista.HideElements(movible.Ocultos);

        // Dentro del recorte, que va justo al alto de la pila.
        double adentro = 0.02 * M;
        var marcaA = new PuntoModelo(bordeIzq + adentro, zInf + adentro);
        var marcaB = new PuntoModelo(bordeDer - adentro, zSup - adentro);
        GeneradorPrevia.Marcar(doc, vista, enPlano, derecha, marcaA);
        GeneradorPrevia.Marcar(doc, vista, enPlano, derecha, marcaB);
        doc.Regenerate();
        movible.Marcas = (marcaA, marcaB);
    }
}
