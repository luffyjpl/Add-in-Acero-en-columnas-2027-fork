#if DEBUG
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// Prueba automática en Revit real (paso 10 del plan de #246): abre el modelo de <c>CQEING_ZAPATAS_PRUEBA</c>, genera el
/// acero con <see cref="GeneradorZapatas"/>, mide cada barra y lo deshace todo. Debe ser una copia: nunca se guarda.
/// </summary>
internal static class PruebaAutomatica
{
    private const string Variable = "CQEING_ZAPATAS_PRUEBA";
    private const double TolMm = 1;

    private static readonly string Ruta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CQEING", "zapatas", "prueba.txt");

    private static readonly StringBuilder S = new();
    private static int pasan, fallan;
    private static readonly Reglas R = Reglas.Generales;

    public static void Registrar(UIControlledApplication app)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
            app.Idling += AlEstarLibre;
    }

    private static void AlEstarLibre(object? sender, IdlingEventArgs e)
    {
        var ui = (UIApplication)sender!;
        ui.Idling -= AlEstarLibre;
        ui.DialogBoxShowing += SinDialogos;
        try
        {
            string modelo = Environment.GetEnvironmentVariable(Variable)!;
            S.AppendLine($"Prueba {DateTime.Now:s} | Revit {ui.Application.VersionNumber}.{ui.Application.SubVersionNumber} | {modelo}");
            UIDocument abierto = ui.OpenAndActivateDocument(modelo);
            Ejecutar(abierto.Document, abierto.ActiveView);
            S.AppendLine().AppendLine($"RESULTADO: {pasan} pasan, {fallan} fallan");
        }
        catch (Exception ex)
        {
            S.AppendLine($"ERROR GENERAL: {ex}");
        }
        S.AppendLine("FIN");
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, S.ToString());
    }

    private static void SinDialogos(object? sender, DialogBoxShowingEventArgs e)
    {
        S.AppendLine($"  [diálogo {e.DialogId}{(e is TaskDialogShowingEventArgs t ? $": {t.Message}" : "")}]");
        e.OverrideResult(1);
    }

    private static void Ejecutar(Document doc, View vista)
    {
        List<ZapataLeida> todas = LectorZapatas.Leer(doc);
        S.AppendLine($"Zapatas: {todas.Count} | Vista: {vista.Name} ({vista.GetType().Name})");
        bool SinAcero(ZapataLeida z) => Contar(doc, z).Total == 0;
        ZapataLeida? cuadrada = todas.FirstOrDefault(z => z.Forma == FormaZapata.Cuadrada && SinAcero(z));
        ZapataLeida? rectangular = todas.FirstOrDefault(z => z.Forma == FormaZapata.Rectangular && SinAcero(z));
        ZapataLeida? aMano = todas.FirstOrDefault(z => !SinAcero(z));
        int rebarAntes = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).GetElementCount();
        ConfigParrillas Cfg(double rec = 75, int n = 5, double sep = 150, bool sup = true, Terminacion t = Terminacion.Pata90, LargoPata largo = LargoPata.Cuadro) =>
            new(new ConfigDireccion(n, sep, t), new ConfigDireccion(n, sep, t), Recubrimientos.Global(rec), sup, LargoPata: largo);
        TiposElegidos Auto = TiposElegidos.Automaticos;

        if (cuadrada is not null)
            Caso(doc, "1 Cuadrada 1.80 x 1.80", () => Generar(doc, vista, [cuadrada], Cfg(), Auto));
        if (rectangular is not null)
        {
            Caso(doc, "2 Rectangular 0.90 x 2.00, principal abajo", () =>
            {
                Generar(doc, vista, [rectangular], Cfg(), Auto);
                Revisar("principal es la dirección del lado largo", MotorParrillas.Principal(rectangular.Entrada) == Direccion.Y, MotorParrillas.Principal(rectangular.Entrada).ToString());
            });
            Caso(doc, "3 Sin parrilla superior", () => Generar(doc, vista, [rectangular], Cfg(sup: false), Auto));
            Caso(doc, "4 Volver a generar no duplica", () =>
            {
                Generar(doc, vista, [rectangular], Cfg(), Auto);
                int antes = Contar(doc, rectangular).Propias;
                Generar(doc, vista, [rectangular], Cfg(), Auto);
                Revisar("mismos conjuntos al regenerar", Contar(doc, rectangular).Propias == antes, $"{antes} y {Contar(doc, rectangular).Propias}");
            });
            Caso(doc, "5 Barra #8 y separación grande", () => Generar(doc, vista, [rectangular], Cfg(n: 8, sep: 300), Auto));
        }
        if (aMano is not null)
            Caso(doc, $"6 Con barras a mano ({aMano.Id})", () =>
            {
                (int total, _) = Contar(doc, aMano);
                Generar(doc, vista, [aMano], Cfg(), Auto, chocarConMano: false);
                (int despues, int propias) = Contar(doc, aMano);
                Revisar("las barras a mano siguen", despues - propias == total, $"{total} antes, {despues - propias} después");
                Generar(doc, vista, [aMano], Cfg(), Auto, chocarConMano: false);
                (despues, propias) = Contar(doc, aMano);
                Revisar("siguen tras regenerar", despues - propias == total, $"{total} antes, {despues - propias} después");
            });
        foreach (IReadOnlyList<string> grupo in AgrupadorZapatas.Agrupar(todas.Select(z => new ZapataParaAgrupar(z.Id, z.Contorno, z.Altura))).Take(2))
        {
            List<ZapataLeida> iguales = todas.Where(z => grupo.Contains(z.Id) && SinAcero(z)).ToList();
            if (iguales.Count > 1)
                Caso(doc, $"7 Zapatas iguales de un clic ({iguales.Count})", () => Generar(doc, vista, iguales, Cfg(), Auto));
        }
        if (rectangular is not null)
        {
            Caso(doc, "8 Recubrimientos por cara 60/70/50", () =>
            {
                ConfigParrillas c = Cfg() with { Recubrimientos = new Recubrimientos(60, 70, 50) };
                double antes = CoverCim(doc);
                Generar(doc, vista, [rectangular], c, Auto);
                Element e = doc.GetElement(Id(rectangular.Id));
                Revisar("la zapata toma 60/70/50", Cover(doc, e, BuiltInParameter.CLEAR_COVER_BOTTOM) == 60 && Cover(doc, e, BuiltInParameter.CLEAR_COVER_OTHER) == 70 && Cover(doc, e, BuiltInParameter.CLEAR_COVER_TOP) == 50, "");
                Revisar("el tipo CIM no cambió", CoverCim(doc) == antes, $"{antes} y {CoverCim(doc)}");
            });
            string? f17 = new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().FirstOrDefault(f => f.Name == "17")?.Id.ToString();
            if (f17 is not null)
                Caso(doc, "9 Forma 17 elegida", () =>
                {
                    Generar(doc, vista, [rectangular], Cfg(), new TiposElegidos(f17, f17, null, null));
                    foreach (Rebar b in Propias(doc, rectangular))
                        S.AppendLine($"    forma usada: {doc.GetElement(b.GetShapeId())?.Name}");
                });
            foreach ((string nombre, Terminacion t) in new[] { ("Estándar - 135°.", Terminacion.Gancho135), ("Estándar - 180°.", Terminacion.Gancho180) })
            {
                string? g = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>().FirstOrDefault(h => h.Name == nombre)?.Id.ToString();
                if (g is not null)
                    Caso(doc, $"10 Gancho {nombre}", () => Generar(doc, vista, [rectangular], Cfg(t: t), new TiposElegidos(null, null, g, g), medirPatas: false));
            }
            Caso(doc, "11 Recta sin gancho", () => Generar(doc, vista, [rectangular], Cfg(t: Terminacion.Recta), Auto, medirPatas: false));

            // Plan de 2026-11-05, paso 8: forma L, ganchos distintos por parrilla y los dos largos de pata.
            string? f17A = new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Cast<RebarShape>().FirstOrDefault(f => f.Name == "17A")?.Id.ToString();
            if (f17A is not null)
                Caso(doc, "16 Forma L (17A): una pata, al inicio", () =>
                {
                    Generar(doc, vista, [rectangular], Cfg(), new TiposElegidos(f17A, f17A, null, null));
                    foreach (Rebar b in Propias(doc, rectangular))
                        S.AppendLine($"    forma usada: {doc.GetElement(b.GetShapeId())?.Name}");
                });
            string? g135 = new FilteredElementCollector(doc).OfClass(typeof(RebarHookType)).Cast<RebarHookType>().FirstOrDefault(h => h.Name == "Estándar - 135°.")?.Id.ToString();
            if (g135 is not null)
                Caso(doc, "17 Ganchos distintos: abajo pata de 90°, arriba 135°", () =>
                {
                    var d = new ConfigDireccion(5, 150, Superior: new Extremos(Terminacion.Gancho135));
                    Generar(doc, vista, [rectangular], new ConfigParrillas(d, d, Recubrimientos.Global(75)), new TiposElegidos(null, null, null, null, GanchoSuperiorX: g135, GanchoSuperiorY: g135));
                });
            Caso(doc, "18 Pata hasta el recubrimiento", () => Generar(doc, vista, [rectangular], Cfg(largo: LargoPata.Recubrimiento), Auto));
            // Decisión 43: la barra cerrada en el corte, con la forma de estribo del catálogo (T1 en el modelo de prueba).
            string? cerrada = TiposZapata.Catalogo(doc, R).Formas.FirstOrDefault(f => f.Clase == ClaseForma.Cerrada)?.Id;
            if (cerrada is not null)
            {
                Caso(doc, "20 Barra cerrada en X, Y con patas y parrilla superior", () =>
                {
                    ConfigParrillas c = Cfg();
                    Generar(doc, vista, [rectangular], c with { X = c.X with { Terminacion = Terminacion.Cerrada } }, new TiposElegidos(cerrada, null, null, null));
                    foreach (Rebar b in Propias(doc, rectangular))
                        S.AppendLine($"    forma usada: {doc.GetElement(b.GetShapeId())?.Name}");
                });
                Caso(doc, "21 Barra cerrada en las dos direcciones", () =>
                {
                    ConfigParrillas c = Cfg();
                    c = c with { X = c.X with { Terminacion = Terminacion.Cerrada }, Y = c.Y with { Terminacion = Terminacion.Cerrada } };
                    Generar(doc, vista, [rectangular], c, new TiposElegidos(cerrada, cerrada, null, null));
                });
            }
        }
        Caso(doc, "19 Zapata de 0.80 m: pata según el cuadro, sin cortar", () =>
        {
            ZapataLeida? alta = CrearLosa(doc, [new(0, 0), new(1800, 0), new(1800, 1800), new(0, 1800)], altura: 800);
            if (alta is not null)
                Generar(doc, vista, [alta], Cfg(), Auto);
        });
        foreach ((string nombre, Punto[] forma) in new[]
                 {
                     ("12 Losa en L", new Punto[] { new(0, 0), new(3000, 0), new(3000, 1000), new(1000, 1000), new(1000, 3000), new(0, 3000) }),
                     ("13 Losa en trapecio", new Punto[] { new(0, 0), new(3000, 0), new(2400, 1500), new(600, 1500) }),
                     ("14 Losa rectangular larga", new Punto[] { new(0, 0), new(4000, 0), new(4000, 1200), new(0, 1200) }),
                 })
            Caso(doc, nombre, () =>
            {
                ZapataLeida? losa = CrearLosa(doc, forma, columnas: nombre.Contains("larga"));
                if (losa is null)
                    return;
                S.AppendLine($"    leída: {losa.Forma} {losa.Tipo} h {losa.Altura:0}, {losa.Columnas.Count} columnas");
                Generar(doc, vista, [losa], Cfg(), Auto);
            });

        Revisar("15 Se deshizo todo: mismas barras que al abrir", new FilteredElementCollector(doc).OfClass(typeof(Rebar)).GetElementCount() == rebarAntes, $"{rebarAntes}");
    }

    /// <summary>Corre un caso dentro de un grupo que siempre se deshace.</summary>
    private static void Caso(Document doc, string nombre, Action accion)
    {
        S.AppendLine().AppendLine($"== {nombre}");
        using var grupo = new TransactionGroup(doc, "Prueba zapatas");
        grupo.Start();
        try
        {
            accion();
        }
        catch (Exception ex)
        {
            Revisar("sin excepciones", false, ex.Message);
        }
        grupo.RollBack();
    }

    private static void Revisar(string que, bool ok, string detalle)
    {
        if (ok) pasan++;
        else fallan++;
        S.AppendLine($"  {(ok ? "PASA " : "FALLA")} {que}{(string.IsNullOrEmpty(detalle) ? "" : $" [{detalle}]")}");
    }

    private static void Generar(Document doc, View vista, List<ZapataLeida> zapatas, ConfigParrillas c, TiposElegidos e, bool medirPatas = true, bool chocarConMano = true)
    {
        ResumenGeneracion g = GeneradorZapatas.Generar(doc, zapatas, c, e, R, vista);
        Revisar("generó", g.Hecho, $"{g.Barras} barras en {g.Conjuntos} conjuntos");
        foreach (string f in g.Fallas)
            S.AppendLine($"    aviso del generador: {f}");
        if (!g.Hecho)
            return;
        foreach (ZapataLeida z in zapatas)
            Medir(doc, z, ProtocoloZapatas.ConTipos(c, e, TiposZapata.Catalogo(doc, R)), medirPatas);
    }

    /// <summary>Compara cada barra en Revit con el plan del motor, sus recubrimientos y los choques entre ellas.</summary>
    private static void Medir(Document doc, ZapataLeida z, ConfigParrillas c, bool medirPatas)
    {
        ResultadoZapata plan = MotorParrillas.Calcular(z.Entrada, c, R);
        var medidas = new List<(double D, List<(double X, double Y, double H)> Puntos)>();
        foreach (Rebar r in Propias(doc, z))
        {
            double d = LectorZapatas.Mm(((RebarBarType)doc.GetElement(r.GetTypeId())).BarNominalDiameter);
            for (int i = 0; i < r.NumberOfBarPositions; i++)
            {
                var puntos = new List<(double, double, double)>();
                foreach (Curve curva in r.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, i))
                    foreach (XYZ p in curva.Tessellate())
                    {
                        Punto local = Geometria.Girar([new Punto(LectorZapatas.Mm(p.X), LectorZapatas.Mm(p.Y))], -z.Angulo)[0];
                        if (puntos.Count == 0 || Math.Abs(puntos[^1].Item1 - local.X) + Math.Abs(puntos[^1].Item2 - local.Y) + Math.Abs(puntos[^1].Item3 - (LectorZapatas.Mm(p.Z) - z.ZBase)) > 1e-6)
                            puntos.Add((local.X, local.Y, LectorZapatas.Mm(p.Z) - z.ZBase));
                    }
                medidas.Add((d, puntos));
            }
        }
        int esperadas = plan.Conjuntos.Sum(b => b.Cantidad);
        Revisar($"{z.Id}: cantidad de barras", medidas.Count == esperadas, $"{medidas.Count} de {esperadas}");

        // Cada barra del plan debe tener una en Revit en su dirección, posición y altura.
        int sinPar = 0;
        double peor = 0;
        foreach (ConjuntoBarras b in plan.Conjuntos)
            foreach (double t in b.Posiciones)
            {
                double mejor = medidas.Select(m => DistanciaAlPlan(m.Puntos, b, t)).DefaultIfEmpty(double.MaxValue).Min();
                peor = Math.Max(peor, mejor);
                if (mejor > TolMm)
                    sinPar++;
            }
        Revisar($"{z.Id}: cada barra en su punto del plan", sinPar == 0, $"{sinPar} corridas, la peor {peor:0.##} mm");

        // Recubrimientos: la cara de la barra, medida contra cada cara de la zapata.
        double abajo = double.MaxValue, arriba = double.MaxValue, lados = double.MaxValue;
        foreach ((double d, List<(double X, double Y, double H)> puntos) in medidas)
            for (int k = 0; k < puntos.Count; k++)
            {
                (double x, double y, double h) = puntos[k];
                // En la punta, la barra termina: a lo largo de su eje no hay medio diámetro que sumar.
                var vecino = puntos[k == 0 ? 1 : k - 1];
                bool punta = k == 0 || k == puntos.Count - 1;
                bool vertical = Math.Abs(h - vecino.H) > Math.Abs(x - vecino.X) + Math.Abs(y - vecino.Y);
                double enAltura = punta && vertical ? 0 : d / 2, enPlanta = punta && !vertical ? 0 : d / 2;
                abajo = Math.Min(abajo, h - enAltura);
                arriba = Math.Min(arriba, z.Altura - h - enAltura);
                lados = Math.Min(lados, DistanciaBorde(z.Contorno, x, y) - enPlanta);
            }
        Recubrimientos rec = c.Recubrimientos;
        Revisar($"{z.Id}: dentro del recubrimiento", abajo >= rec.Inferior - TolMm && arriba >= rec.Superior - TolMm && lados >= rec.Lateral - TolMm,
            $"abajo {abajo:0.#}/{rec.Inferior:0} arriba {arriba:0.#}/{rec.Superior:0} lados {lados:0.#}/{rec.Lateral:0}");

        if (medirPatas)
        {
            // Cada punta con pata o gancho va hacia adentro (arriba en la inferior, abajo en la superior); la L lleva una
            // sola pata, al inicio, y la pata mide lo que pide el motor (decisiones 29 y 37 del plan de 2026-11-05).
            int mal = 0;
            double peorPata = 0;
            foreach (ConjuntoBarras b in plan.Conjuntos.Where(b => b.Terminacion != Terminacion.Recta))
                foreach (double t in b.Posiciones)
                {
                    var m = medidas.MinBy(x => DistanciaAlPlan(x.Puntos, b, t)).Puntos;
                    if (b.Terminacion == Terminacion.Cerrada)
                    {
                        // La cerrada (decisión 43): llega arriba a su altura y las dos colas quedan adentro, del lado del
                        // inicio, arriba de la mitad de su alto menos un poco (H21: 143 y 191 mm en una de 0.35 m).
                        double A((double X, double Y, double H) p) => b.Direccion == Direccion.X ? p.X : p.Y;
                        bool llegaArriba = Math.Abs(m.Max(p => p.H) - b.ZArriba!.Value) <= TolMm;
                        bool colas = new[] { m[0], m[^1] }.All(p => A(p) > b.Desde && A(p) < b.Desde + (b.Hasta - b.Desde) / 2
                            && p.H > b.Z + b.Diametro && p.H < b.ZArriba.Value);
                        if (!llegaArriba || !colas)
                            mal++;
                        continue;
                    }
                    double s = b.Parrilla == Parrilla.Inferior ? 1 : -1;
                    var puntas = new[] { m[0], m[^1] }.Where(p => s * (p.H - b.Z) > 1).ToList();
                    bool bien = puntas.Count == (b.Terminacion == Terminacion.Pata90 && b.Patas == Patas.Inicio ? 1 : 2);
                    if (bien && b.Terminacion == Terminacion.Pata90)
                    {
                        foreach (var p in puntas)
                            peorPata = Math.Max(peorPata, Math.Abs(Math.Abs(p.H - b.Z) - (b.Pata - b.Diametro / 2)));
                        if (b.Patas == Patas.Inicio)
                            bien = Math.Abs((b.Direccion == Direccion.X ? puntas[0].X : puntas[0].Y) - (b.Desde + b.Diametro / 2)) <= TolMm;
                    }
                    if (!bien)
                        mal++;
                }
            Revisar($"{z.Id}: patas y ganchos hacia adentro, en su punta y con su largo", mal == 0 && peorPata <= TolMm, $"{mal} mal; la pata más lejos del plan, a {peorPata:0.#} mm");
        }

        // Choques: distancia entre ejes contra la suma de radios, para cada par de barras.
        int choques = 0;
        double holgura = double.MaxValue;
        string peorPar = "";
        for (int i = 0; i < medidas.Count; i++)
            for (int j = i + 1; j < medidas.Count; j++)
            {
                double dist = DistanciaEntre(medidas[i].Puntos, medidas[j].Puntos) - (medidas[i].D + medidas[j].D) / 2;
                if (dist < holgura)
                {
                    holgura = dist;
                    peorPar = $"{Rumbo(medidas[i].Puntos)} y {Rumbo(medidas[j].Puntos)}";
                }
                if (dist < -0.5)
                    choques++;
            }
        Revisar($"{z.Id}: barras sin atravesarse", choques == 0, $"{choques} pares; holgura mínima {holgura:0.#} mm entre {peorPar}");
        // Los choques que marca el tablero (TrazosZapata, decisión 31) contra los medidos aquí: deben aparecer en los mismos casos.
        IReadOnlyList<ChoqueZapata> delTablero = TrazosZapata.Choques(plan.Conjuntos, TrazosZapata.Trazos(plan.Conjuntos, R, c.Doblados, c.DobladosEstribo));
        Revisar($"{z.Id}: el tablero marca choques cuando Revit los tiene", (choques == 0) == (delTablero.Count == 0),
            $"tablero {delTablero.Count} lugares, peor {(delTablero.Count > 0 ? delTablero.Max(x => x.SeMeteMm) : 0):0.#} mm; Revit {choques} pares");
        foreach (string a in plan.Avisos)
            S.AppendLine($"    aviso del motor: {a}");
    }

    /// <summary>Para el informe: la dirección, la altura y la posición transversal del tramo más largo de la barra.</summary>
    private static string Rumbo(List<(double X, double Y, double H)> p)
    {
        var tramo = Enumerable.Range(0, p.Count - 1).Select(i => (A: p[i], B: p[i + 1])).MaxBy(s => Math.Abs(s.B.X - s.A.X) + Math.Abs(s.B.Y - s.A.Y));
        bool enX = Math.Abs(tramo.B.X - tramo.A.X) > Math.Abs(tramo.B.Y - tramo.A.Y);
        return $"{(enX ? "X" : "Y")} z{tramo.A.H:0} en {(enX ? tramo.A.Y : tramo.A.X):0}";
    }

    /// <summary>Qué tan lejos queda una barra medida de la recta planeada, en el plano perpendicular a su dirección.</summary>
    private static double DistanciaAlPlan(List<(double X, double Y, double H)> puntos, ConjuntoBarras b, double t)
    {
        double mejor = double.MaxValue;
        for (int i = 0; i + 1 < puntos.Count; i++)
        {
            var (p, q) = (puntos[i], puntos[i + 1]);
            bool enX = Math.Abs(q.X - p.X) > Math.Abs(q.Y - p.Y);
            if (Math.Abs(q.H - p.H) > 1 || enX != (b.Direccion == Direccion.X) || Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y) < 50)
                continue;
            double trans = b.Direccion == Direccion.X ? p.Y : p.X;
            mejor = Math.Min(mejor, Math.Sqrt((trans - t) * (trans - t) + (p.H - b.Z) * (p.H - b.Z)));
        }
        return mejor;
    }

    private static double DistanciaBorde(IReadOnlyList<Punto> c, double x, double y)
    {
        double mejor = double.MaxValue;
        for (int i = 0; i < c.Count; i++)
        {
            Punto a = c[i], b = c[(i + 1) % c.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            mejor = Math.Min(mejor, Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(y - a.Y - t * dy, 2)));
        }
        return mejor;
    }

    private static double DistanciaEntre(List<(double X, double Y, double H)> a, List<(double X, double Y, double H)> b)
    {
        double mejor = double.MaxValue;
        for (int i = 0; i + 1 < a.Count; i++)
            for (int j = 0; j + 1 < b.Count; j++)
                mejor = Math.Min(mejor, Segmentos(a[i], a[i + 1], b[j], b[j + 1]));
        return mejor;
    }

    /// <summary>Distancia entre dos segmentos en el espacio (método de Ericson).</summary>
    private static double Segmentos((double X, double Y, double H) p1, (double X, double Y, double H) q1, (double X, double Y, double H) p2, (double X, double Y, double H) q2)
    {
        (double, double, double) Resta((double X, double Y, double H) u, (double X, double Y, double H) v) => (u.X - v.X, u.Y - v.Y, u.H - v.H);
        double Punto3((double, double, double) u, (double, double, double) v) => u.Item1 * v.Item1 + u.Item2 * v.Item2 + u.Item3 * v.Item3;
        var d1 = Resta(q1, p1);
        var d2 = Resta(q2, p2);
        var r = Resta(p1, p2);
        double a = Punto3(d1, d1), e = Punto3(d2, d2), f = Punto3(d2, r), c = Punto3(d1, r), b = Punto3(d1, d2);
        double den = a * e - b * b;
        double s = a <= 1e-9 ? 0 : den > 1e-9 ? Math.Clamp((b * f - c * e) / den, 0, 1) : 0;
        double t = e <= 1e-9 ? 0 : (b * s + f) / e;
        if (t < 0)
        {
            t = 0;
            s = a <= 1e-9 ? 0 : Math.Clamp(-c / a, 0, 1);
        }
        else if (t > 1)
        {
            t = 1;
            s = a <= 1e-9 ? 0 : Math.Clamp((b - c) / a, 0, 1);
        }
        double x = p1.X + d1.Item1 * s - (p2.X + d2.Item1 * t), y = p1.Y + d1.Item2 * s - (p2.Y + d2.Item2 * t), h = p1.H + d1.Item3 * s - (p2.H + d2.Item3 * t);
        return Math.Sqrt(x * x + y * y + h * h);
    }

    /// <summary>Una losa de cimentación de prueba, para las formas que el modelo no trae. Se deshace con el caso.</summary>
    private static ZapataLeida? CrearLosa(Document doc, Punto[] forma, bool columnas = false, double altura = 500)
    {
        FloorType? base0 = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().FirstOrDefault(t => t.IsFoundationSlab);
        Level? nivel = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
        if (base0 is null || nivel is null)
        {
            S.AppendLine("    SIN PROBAR: el modelo no tiene un tipo de losa de cimentación.");
            return null;
        }
        double desplazamiento = 60000;
        var lazo = new CurveLoop();
        for (int i = 0; i < forma.Length; i++)
        {
            Punto a = forma[i], b = forma[(i + 1) % forma.Length];
            lazo.Append(Line.CreateBound(new XYZ(LectorZapatas.Pies(a.X + desplazamiento), LectorZapatas.Pies(a.Y), nivel.Elevation), new XYZ(LectorZapatas.Pies(b.X + desplazamiento), LectorZapatas.Pies(b.Y), nivel.Elevation)));
        }
        Floor losa;
        using (var t = new Transaction(doc, "Prueba: losa"))
        {
            t.Start();
            // El tipo del modelo mide 150 mm: uno de prueba de la altura pedida (500 por defecto), que se deshace con el caso.
            var tipo = (FloorType)base0.Duplicate($"CQEING Prueba {Guid.NewGuid():N}");
            CompoundStructure capas = tipo.GetCompoundStructure();
            capas.SetLayerWidth(capas.GetFirstCoreLayerIndex(), LectorZapatas.Pies(altura));
            tipo.SetCompoundStructure(capas);
            losa = Floor.Create(doc, new List<CurveLoop> { lazo }, tipo.Id, nivel.Id, true, null, 0);
            if (columnas)
                foreach (Punto c in new[] { new Punto(600, 600), new Punto(forma.Max(p => p.X) - 600, 600) })
                {
                    FamilySymbol? simbolo = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralColumns).Cast<FamilySymbol>().FirstOrDefault();
                    if (simbolo is null)
                        continue;
                    if (!simbolo.IsActive)
                        simbolo.Activate();
                    doc.Create.NewFamilyInstance(new XYZ(LectorZapatas.Pies(c.X + desplazamiento), LectorZapatas.Pies(c.Y), nivel.Elevation), simbolo, nivel, Autodesk.Revit.DB.Structure.StructuralType.Column);
                }
            t.Commit();
        }
        return LectorZapatas.Leer(doc, losa, []);
    }

    private static List<Rebar> Propias(Document doc, ZapataLeida z) =>
        RebarHostData.GetRebarHostData(doc.GetElement(Id(z.Id)))?.GetRebarsInHost().Where(MarcaZapata.EsPropia).ToList() ?? [];

    private static (int Total, int Propias) Contar(Document doc, ZapataLeida z) => MarcaZapata.Contar(doc.GetElement(Id(z.Id)));

    private static ElementId Id(string id) => new(long.Parse(id));

    private static double Cover(Document doc, Element e, BuiltInParameter cara) =>
        doc.GetElement(e.get_Parameter(cara).AsElementId()) is RebarCoverType t ? Math.Round(LectorZapatas.Mm(t.CoverDistance), 1) : -1;

    private static double CoverCim(Document doc) => new FilteredElementCollector(doc).OfClass(typeof(RebarCoverType)).Cast<RebarCoverType>()
        .Where(t => t.Name == "CIM").Select(t => Math.Round(LectorZapatas.Mm(t.CoverDistance), 1)).FirstOrDefault(-1);
}
#endif
