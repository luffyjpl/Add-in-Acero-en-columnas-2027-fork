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

    /// <summary>Como la vista larga de la viga.</summary>
    private const int Escala = 15;

    /// <summary>El recorte deja ver esto a cada lado de la columna: el arranque de sus vigas.</summary>
    private const double Lado = 0.30 * M;
    private const double Holgura = 0.005 * M;

    /// <summary>Del borde derecho del recorte al primer carril de barras sacadas.</summary>
    private const double InicioSacadas = 0.25 * M;

    /// <summary>Entre dos piezas del mismo carril (una encima de otra) y entre carriles.</summary>
    private const double HuecoEnCarril = 0.10 * M;
    private const double HuecoCarriles = 0.12 * M;

    /// <summary>Lo que se corre a un lado la pieza que traslapa con la de abajo, como en la viga.</summary>
    private const double DesfaseTraslapo = 0.08 * M;

    // A 1:15 la caja de la nota baja 0.09 m desde su punto y mide 0.12 m (medido en la viga, 30-oct).
    private const double AltoTexto = 0.11 * M;
    private const double BajaNota = 0.09 * M;
    private const double AireNota = 0.01 * M;
    private const double AnchoNotaGirada = 0.10 * M;
    private const double FilaEstribos = 0.13 * M;
    private const double MismaZona = 0.10 * M;

    /// <summary>De la etiqueta de estribos al borde izquierdo del recorte.</summary>
    private const double AireEstribos = 0.05 * M;

    /// <summary>A la izquierda del recorte: la cadena de elementos y, más afuera, el alto total.</summary>
    private const double CotaElementos = 0.45 * M;
    private const double CotaTotal = 0.80 * M;
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
            t.RollBack();
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

    /// <summary>Una barra sacada: el detalle, lo que ocupa (en coordenadas de la vista) y sus textos.</summary>
    private sealed class Sacada(Rebar barra, Element detalle, XYZ posicion, int cantidad, HashSet<ElementId> iguales)
    {
        public Rebar Barra { get; } = barra;
        public Element Detalle { get; } = detalle;

        /// <summary>Donde se creó el detalle: de ahí se mueve.</summary>
        public XYZ Posicion { get; } = posicion;
        public int Cantidad { get; } = cantidad;

        /// <summary>Todas las barras que dibuja: para saber de qué varilla es pieza.</summary>
        public HashSet<ElementId> Iguales { get; } = iguales;
        public double Izquierda { get; set; }
        public double Abajo { get; set; }
        public double Ancho { get; set; }
        public double Alto { get; set; }
        public XYZ Final { get; set; } = XYZ.Zero;
        public TextNote? Etiqueta { get; set; }
        public List<TextNote> Anexos { get; } = [];
    }

    private static View Alzado(Document doc, ColumnaLeida columna, TiposPropios tipos, DespieceMovible movible)
    {
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

        List<Sacada> sacadas = Sacar(doc, vista, columna, barras, derecha, bordeDer, En, tipos);
        var sueltas = new List<(Rebar Barra, string Clave, TextNote Nota)>();
        EtiquetarEstribos(doc, vista, barras, bordeIzq, En, tipos, sueltas);

        GeneradorPrevia.Recortar(vista, centro, centro.DotProduct(derecha) - bordeIzq, bordeDer - centro.DotProduct(derecha),
            centro.Z - min.Z + Holgura, max.Z - centro.Z + Holgura);
        doc.Regenerate();
        Cotar(doc, vista, columna, bordeIzq, En, tipos);
        vista.DisplayStyle = DisplayStyle.FlatColors;
        AplicarMovimientos(doc, sacadas, sueltas, movible, derecha);
        if (movible.Separar)
            Separar(doc, vista, sacadas, sueltas, movible, derecha, enPlano, bordeIzq, bordeDer, min.Z, max.Z);
        return vista;
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
    private static List<Sacada> Sacar(Document doc, View vista, ColumnaLeida columna, List<Rebar> barras, XYZ derecha, double bordeDer,
        Func<double, double, XYZ> En, TiposPropios tipos)
    {
        var creadas = new List<Sacada>();
        foreach (IGrouping<string, Rebar> iguales in barras.Where(LectorColumnas.EsVertical).GroupBy(b =>
            $"{b.GetTypeId()}|{b.GetShapeId()}|{Math.Round(LectorDespiece.LargoBarra(b) / AlCentimetro)}|{Math.Round(LectorColumnas.Alturas(b).Abajo / AlCentimetro)}"))
        {
            // La que tenga los ganchos de lado: un gancho que apunta hacia quien mira no se dibuja (hallazgo 25).
            Rebar b = iguales.OrderByDescending(x => GanchosDeLado(x, derecha)).First();
            if (GanchosDeLado(b, derecha) < 0.5)
                Registro.Paso($"columna: ningún gancho de lado en las iguales a la barra {b.Id}");
            (double abajo, double arriba) = LectorColumnas.Alturas(b);
            try
            {
                XYZ p = En(bordeDer + 3 * M, (abajo + arriba) / 2);
                Element d = RebarBendingDetail.Create(doc, vista.Id, b.Id, 0, tipos.BarraSinMedidas, p, 0);
                GeneradorPrevia.Pintar(vista, d.Id, b, 2);
                creadas.Add(new Sacada(b, d, p, iguales.Sum(x => x.Quantity), iguales.Select(x => x.Id).ToHashSet()));
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
            BoundingBoxXYZ m = s.Detalle.get_BoundingBox(vista);
            double a = m.Min.DotProduct(derecha), e = m.Max.DotProduct(derecha);
            s.Izquierda = Math.Min(a, e);
            s.Ancho = Math.Abs(e - a);
            s.Alto = m.Max.Z - m.Min.Z;
            // A su altura real: el borde de abajo del detalle donde está el de la barra.
            double d = ((RebarBarType)doc.GetElement(s.Barra.GetTypeId())).BarNominalDiameter;
            s.Abajo = LectorColumnas.Alturas(s.Barra).Abajo - d / 2;
            s.Final = s.Posicion + XYZ.BasisZ * (s.Abajo - m.Min.Z);
        }

        List<List<Sacada>> lineas = Lineas(columna, creadas);
        // Cada línea: a la izquierda la medida del traslapo (si tiene), luego las piezas corridas y a la derecha sus etiquetas.
        double Izquierdo(List<Sacada> l) => l.Count > 1 ? AnchoNotaGirada + AireNota : 0;
        double Desfase(int j) => j % 2 * DesfaseTraslapo;
        IEnumerable<PiezaFila> piezas = lineas.Select((l, i) => new PiezaFila(i,
            l.Min(s => s.Abajo) - AltoTexto, l.Max(s => s.Abajo + s.Alto) + AltoTexto,
            Izquierdo(l) + l.Select((s, j) => Desfase(j) + s.Ancho).Max() + AireNota + AnchoNotaGirada));
        foreach (PiezaUbicada u in AcomodadorFilas.Acomodar(piezas, bordeDer + InicioSacadas, true, HuecoEnCarril, HuecoCarriles).Piezas)
        {
            List<Sacada> linea = lineas[u.Pieza.Indice];
            double inicio = u.Piso + Izquierdo(linea);
            for (int j = 0; j < linea.Count; j++)
            {
                Sacada s = linea[j];
                double x = inicio + Desfase(j);
                s.Final += derecha * (x - s.Izquierda);
                s.Izquierda = x;
                RebarBendingDetail.SetPosition(s.Detalle, s.Final);

                string tipo = doc.GetElement(s.Barra.GetTypeId()).Name;
                string texto = LectorDespiece.Etiqueta(tipo, s.Cantidad, LectorDespiece.LargoBarra(s.Barra), GeneradorPrevia.Curvas(s.Barra, true).Sum(c => c.Length));
                s.Etiqueta = TextNote.Create(doc, vista.Id, En(s.Izquierda + s.Ancho + AireNota, s.Abajo + s.Alto / 2), texto, Girada(tipos));
                GeneradorPrevia.Pintar(vista, s.Etiqueta.Id, s.Barra, null);
                MedirGanchos(doc, vista, s, derecha, En, tipos);
                if (j > 0)
                    MedirTraslapo(doc, vista, linea[j - 1], s, inicio, En, tipos);
            }
        }
        doc.Regenerate();
        return creadas;
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
        TextNote nota = TextNote.Create(doc, vista.Id, En(inicio - AireNota - AnchoNotaGirada, (desde + hasta) / 2),
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
        IList<Curve> curvas = GeneradorPrevia.Curvas(s.Barra, false);
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
        foreach (IGrouping<string, Rebar> porForma in barras.Where(b => !LectorColumnas.EsVertical(b))
            .GroupBy(b => $"{b.GetTypeId()}|{b.GetShapeId()}|{Math.Round(LectorDespiece.LargoBarra(b) / AlCentimetro)}"))
        {
            string tipo = doc.GetElement(porForma.First().GetTypeId()).Name;
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
    private static void Cotar(Document doc, View vista, ColumnaLeida columna, double bordeIzq, Func<double, double, XYZ> En, TiposPropios tipos)
    {
        var marcas = new List<double>();
        foreach (BoundingBoxXYZ c in columna.Elementos.Select(id => doc.GetElement(id).get_BoundingBox(null)).Where(c => c is not null))
            foreach (double z in new[] { c.Min.Z, c.Max.Z })
                if (!marcas.Any(m => Math.Abs(m - z) < AlCentimetro))
                    marcas.Add(z);
        marcas.Sort();
        if (marcas.Count < 2)
            return;

        using var prueba = new SubTransaction(doc);
        prueba.Start();
        try
        {
            var blanco = new OverrideGraphicSettings();
            blanco.SetProjectionLineColor(new Color(255, 255, 255));
            List<Reference> planos = marcas.Select(z =>
            {
                ReferencePlane plano = doc.Create.NewReferencePlane(En(bordeIzq + DentroDelRecorte, z), En(bordeIzq - CotaTotal - DentroDelRecorte, z), vista.ViewDirection, vista);
                vista.SetElementOverrides(plano.Id, blanco);
                return plano.GetReference();
            }).ToList();
            doc.Regenerate();

            void Cota(IEnumerable<Reference> referencias, double x)
            {
                var lista = new ReferenceArray();
                foreach (Reference r in referencias)
                    lista.Append(r);
                Dimension cota = doc.Create.NewDimension(vista, Line.CreateBound(En(x, marcas[0]), En(x, marcas[^1])), lista);
                if (tipos.CotaCorta is { } tipo)
                    cota.DimensionType = tipo;
            }
            // Con un solo elemento la cadena y el total dirían lo mismo.
            if (marcas.Count > 2)
                Cota(planos, bordeIzq - CotaElementos);
            Cota([planos[0], planos[^1]], bordeIzq - CotaTotal);
            prueba.Commit();
        }
        catch (Exception e)
        {
            Registro.Paso("columna: cotas no creadas: " + e.Message);
            prueba.RollBack();
        }
    }

    /// <summary>Lo que el usuario movió en el paso 2: la barra a los lados (con su etiqueta y sus ganchos) y los textos libres.</summary>
    private static void AplicarMovimientos(Document doc, List<Sacada> sacadas, List<(Rebar Barra, string Clave, TextNote Nota)> sueltas, DespieceMovible movible, XYZ derecha)
    {
        foreach (Sacada s in sacadas)
        {
            if (!movible.Movimientos.TryGetValue(s.Barra.Id.Value.ToString(), out Movimiento? m))
                continue;
            XYZ barra = derecha * (m.Dx / 0.3048) + XYZ.BasisZ * (m.Dz / 0.3048);
            RebarBendingDetail.SetPosition(s.Detalle, s.Final + barra);
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
        XYZ derecha, XYZ enPlano, double bordeIzq, double bordeDer, double zInf, double zSup)
    {
        (double A, double B) EnX(BoundingBoxXYZ c)
        {
            double a = c.Min.DotProduct(derecha), b = c.Max.DotProduct(derecha);
            return (Math.Min(a, b), Math.Max(a, b));
        }
        TextoMovible Texto(TextNote nota)
        {
            BoundingBoxXYZ c = nota.get_BoundingBox(vista);
            (double a, double b) = EnX(c);
            return new TextoMovible(nota.Text.Trim(), new PuntoModelo((a + b) / 2, (c.Min.Z + c.Max.Z) / 2), Math.Abs(nota.BaseDirection.Z) > 0.5);
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
            List<XYZ> curva = GeneradorPrevia.Curvas(s.Barra, false).SelectMany(c => c.Tessellate()).ToList();
            double dx = izquierda - curva.Min(p => p.DotProduct(derecha)), dz = caja.Min.Z - curva.Min(p => p.Z);
            List<PuntoModelo> puntos = curva.Select(p => new PuntoModelo(p.DotProduct(derecha) + dx, p.Z + dz)).ToList();
            movible.Piezas.Add(new PiezaMovible(s.Barra.Id.Value.ToString(), LectorDespiece.Color(doc.GetElement(s.Barra.GetTypeId()).Name), puntos,
                Texto(s.Etiqueta!), s.Anexos.Select(Texto).ToList()));
            Ocultar(s.Detalle);
            Ocultar(s.Etiqueta!);
            s.Anexos.ForEach(Ocultar);
        }
        foreach ((Rebar barra, string clave, TextNote nota) in sueltas)
        {
            movible.Piezas.Add(new PiezaMovible(clave, LectorDespiece.Color(doc.GetElement(barra.GetTypeId()).Name), [], Texto(nota), []));
            Ocultar(nota);
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
