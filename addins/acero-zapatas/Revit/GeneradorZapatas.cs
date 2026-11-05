using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using CQEING.Vigas.Calculo.Modelo;
using CQEING.Zapatas.Calculo;

namespace CQEING.Zapatas.Revit;

/// <summary>Lo que eligió el modelador en el catálogo: la forma y el gancho por dirección; null es la opción automática.</summary>
internal sealed record EleccionTipos(string? FormaX, string? FormaY, string? GanchoX, string? GanchoY);

/// <summary>El resultado de generar, para el mensaje del tablero.</summary>
internal sealed record ResumenGeneracion(bool Hecho, int Zapatas, int Conjuntos, int Barras, IReadOnlyList<string> Fallas);

/// <summary>
/// Crea en Revit lo que calculó <see cref="MotorParrillas"/> (#254), en una sola transacción: si algo falla no queda nada a
/// medias, y un Ctrl+Z lo deshace de una vez. Lo medido con el diagnóstico está en el plan de #246: una barra en U por
/// curvas (decisión 23) queda en su punto, con la punta exacta en el recubrimiento opuesto.
/// </summary>
internal static class GeneradorZapatas
{
    public const string NombreTransaccion = "Zapatas: generar acero";

    /// <summary>Diferencia con el plan a partir de la cual se avisa, en mm.</summary>
    private const double ToleranciaMm = 1;

    public static ResumenGeneracion Generar(Document doc, IReadOnlyList<ZapataLeida> zapatas, ConfigParrillas config, EleccionTipos eleccion, Reglas reglas, View vista)
    {
        var fallas = new List<string>();
        Dictionary<int, RebarBarType> barras = TiposZapata.Barras(doc, reglas);
        foreach (int numero in new[] { config.X.Numero, config.Y.Numero }.Distinct())
            if (!barras.ContainsKey(numero))
                fallas.Add($"El proyecto no tiene un tipo de barra para el #{numero}.");
        if (fallas.Count > 0)
            return new ResumenGeneracion(false, 0, 0, 0, fallas);

        config = TiposZapata.ConDoblados(doc, config, reglas);
        var manejo = new ManejoFallas();
        using var t = new Transaction(doc, NombreTransaccion);
        FailureHandlingOptions opciones = t.GetFailureHandlingOptions();
        opciones.SetFailuresPreprocessor(manejo);
        opciones.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(opciones);
        t.Start();

        int conjuntos = 0, total = 0;
        try
        {
            foreach (ZapataLeida z in zapatas)
            {
                ResultadoZapata r = MotorParrillas.Calcular(z.Entrada, config, reglas);
                if (r.Errores.Count > 0)
                    throw new InvalidOperationException($"{z.Nombre}: {r.Errores[0]}");
                if (doc.GetElement(Id(z.Id)) is not { } host)
                    throw new InvalidOperationException($"{z.Nombre}: la zapata ya no está en el modelo.");
                MarcaZapata.Borrar(doc, host);
                PonerRecubrimientos(doc, host, config.Recubrimientos);
                foreach (ConjuntoBarras c in r.Conjuntos)
                {
                    bool enX = c.Direccion == Direccion.X;
                    RebarShape? forma = doc.GetElement(Id(enX ? eleccion.FormaX : eleccion.FormaY)) as RebarShape;
                    RebarHookType? gancho = doc.GetElement(Id(enX ? eleccion.GanchoX : eleccion.GanchoY)) as RebarHookType;
                    Rebar barra = Crear(doc, host, z, c, config.Recubrimientos, barras[c.Numero], forma, gancho, fallas)
                        ?? throw new InvalidOperationException($"{z.Nombre}: Revit no pudo crear la parrilla {c.Parrilla} en {c.Direccion}.");
                    MarcaZapata.Marcar(barra, $"{c.Parrilla} {c.Direccion} {c.Capa}");
                    doc.Regenerate();
                    Repartir(barra, z, c, fallas);
                    Revisar(barra, z, c, fallas);
                    Mostrar(barra, vista);
                    conjuntos++;
                    total += c.Cantidad;
                }
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException or Autodesk.Revit.Exceptions.ArgumentException)
        {
            t.RollBack();
            fallas.Add(e.Message);
            return new ResumenGeneracion(false, 0, 0, 0, fallas);
        }

        TransactionStatus estado = t.Commit();
        fallas.AddRange(manejo.Errores);
        return estado == TransactionStatus.Committed
            ? new ResumenGeneracion(true, zapatas.Count, conjuntos, total, fallas)
            : new ResumenGeneracion(false, 0, 0, 0, fallas);
    }

    private static ElementId Id(string? id) => long.TryParse(id, out long n) ? new ElementId(n) : ElementId.InvalidElementId;

    private static void PonerRecubrimientos(Document doc, Element host, Recubrimientos r)
    {
        Poner(BuiltInParameter.CLEAR_COVER_BOTTOM, r.Inferior);
        Poner(BuiltInParameter.CLEAR_COVER_OTHER, r.Lateral);
        Poner(BuiltInParameter.CLEAR_COVER_TOP, r.Superior);

        void Poner(BuiltInParameter cara, double mm)
        {
            if (host.get_Parameter(cara) is not { IsReadOnly: false } p)
                return;
            if (doc.GetElement(p.AsElementId()) is RebarCoverType actual && Math.Abs(LectorZapatas.Mm(actual.CoverDistance) - mm) < 0.5)
                return;
            p.Set(TiposZapata.Recubrimiento(doc, mm).Id);
        }
    }

    private static Rebar? Crear(Document doc, Element host, ZapataLeida z, ConjuntoBarras c, Recubrimientos rec, RebarBarType barra, RebarShape? forma, RebarHookType? gancho, List<string> fallas)
    {
        double y = c.Posiciones[0];
        XYZ normal = Normal(z, c);
        double zBarra = c.Z, d = c.Diametro;
        using var terminaciones = new BarTerminationsData(doc);
        IList<Curve> curvas;
        if (c.Terminacion == Terminacion.Pata90)
        {
            // La punta llega al recubrimiento opuesto; las curvas van por el eje de la barra (decisiones 12 y 23).
            double punta = c.Parrilla == Parrilla.Inferior ? zBarra + c.Pata - d / 2 : zBarra - c.Pata + d / 2;
            double xi = c.Desde + d / 2, xf = c.Hasta - d / 2;
            curvas = new List<Curve>
            {
                Line.CreateBound(Punto(z, c, xi, y, punta), Punto(z, c, xi, y, zBarra)),
                Line.CreateBound(Punto(z, c, xi, y, zBarra), Punto(z, c, xf, y, zBarra)),
                Line.CreateBound(Punto(z, c, xf, y, zBarra), Punto(z, c, xf, y, punta)),
            };
        }
        else
        {
            curvas = new List<Curve> { Line.CreateBound(Punto(z, c, c.Desde, y, zBarra), Punto(z, c, c.Hasta, y, zBarra)) };
            if (c.Terminacion != Terminacion.Recta && gancho is not null)
            {
                terminaciones.HookTypeIdAtStart = gancho.Id;
                terminaciones.HookTypeIdAtEnd = gancho.Id;
                terminaciones.TerminationOrientationAtStart = RebarTerminationOrientation.Right;
                terminaciones.TerminationOrientationAtEnd = RebarTerminationOrientation.Right;
            }
        }
        if (forma is not null && c.Terminacion == Terminacion.Pata90)
        {
            try
            {
                if (Rebar.CreateFromCurvesAndShape(doc, forma, barra, host, normal, curvas, terminaciones) is { } conForma)
                    return conForma;
            }
            catch (Exception e) when (e is Autodesk.Revit.Exceptions.ApplicationException or Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Se usa la automática y se avisa abajo.
            }
            string aviso = $"La forma «{forma.Name}» no admite una barra en U de estas medidas: se usó la que Revit reconoce.";
            if (!fallas.Contains(aviso))
                fallas.Add(aviso);
        }
        Rebar? r = Rebar.CreateFromCurves(doc, RebarStyle.Standard, barra, host, normal, curvas, terminaciones, useExistingShapeIfPossible: true, createNewShape: true);
        if (r is not null && c.Terminacion is Terminacion.Gancho135 or Terminacion.Gancho180)
        {
            doc.Regenerate();
            OrientarGanchos(r, c);
        }
        return r;
    }

    /// <summary>
    /// Deja el gancho hacia adentro: arriba en la parrilla inferior y abajo en la superior. Revit decide qué lado es
    /// «derecha» según la normal; en vez de suponerlo se mira dónde quedó la punta, como en <c>CreadorBarras</c> de vigas.
    /// </summary>
    private static void OrientarGanchos(Rebar r, ConjuntoBarras c)
    {
        IList<Curve> curvas = r.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
        if (curvas.Count < 2)
            return;
        double recta = curvas.OfType<Line>().MaxBy(l => l.Length)!.GetEndPoint(0).Z;
        bool hacia = c.Parrilla == Parrilla.Inferior;
        Voltear(0, curvas[0].GetEndPoint(0).Z);
        Voltear(1, curvas[^1].GetEndPoint(1).Z);
        r.Document.Regenerate();

        void Voltear(int extremo, double punta)
        {
            if (hacia ? punta > recta : punta < recta)
                return;
            RebarTerminationOrientation actual = r.GetTerminationOrientation(extremo);
            r.SetTerminationOrientation(extremo, actual == RebarTerminationOrientation.Right ? RebarTerminationOrientation.Left : RebarTerminationOrientation.Right);
        }
    }

    /// <summary>Reparte las barras hasta la última posición; el sentido se comprueba midiendo, no se supone.</summary>
    private static void Repartir(Rebar r, ZapataLeida z, ConjuntoBarras c, List<string> fallas)
    {
        if (c.Cantidad < 2)
            return;
        double largo = LectorZapatas.Pies(c.Posiciones[^1] - c.Posiciones[0]);
        RebarShapeDrivenAccessor acceso = r.GetShapeDrivenAccessor();
        acceso.SetLayoutAsFixedNumber(c.Cantidad, largo, true, true, true);
        r.Document.Regenerate();
        if (Math.Abs(Posicion(r, z, c, c.Cantidad - 1) - c.Posiciones[^1]) > ToleranciaMm)
        {
            acceso.SetLayoutAsFixedNumber(c.Cantidad, largo, false, true, true);
            r.Document.Regenerate();
        }
        double diferencia = Math.Abs(Posicion(r, z, c, c.Cantidad - 1) - c.Posiciones[^1]);
        if (diferencia > ToleranciaMm)
            fallas.Add($"{z.Nombre}: el reparto {c.Parrilla} en {c.Direccion} quedó {diferencia:0.#} mm corrido.");
    }

    /// <summary>Avisa si Revit dejó la barra en otro punto del plan, o si el reparto no da la cantidad calculada.</summary>
    private static void Revisar(Rebar r, ZapataLeida z, ConjuntoBarras c, List<string> fallas)
    {
        double diferencia = Math.Abs(Posicion(r, z, c, 0) - c.Posiciones[0]);
        if (diferencia > ToleranciaMm)
            fallas.Add($"{z.Nombre}: la primera barra {c.Parrilla} en {c.Direccion} quedó {diferencia:0.#} mm corrida del plan.");
        if (r.NumberOfBarPositions != c.Cantidad)
            fallas.Add($"{z.Nombre}: {c.Parrilla} en {c.Direccion}: Revit dejó {r.NumberOfBarPositions} barras y el plan pide {c.Cantidad}.");
    }

    /// <summary>La coordenada transversal de una barra del conjunto, en mm y en los ejes de la zapata.</summary>
    private static double Posicion(Rebar r, ZapataLeida z, ConjuntoBarras c, int indice)
    {
        Line recta = r.GetTransformedCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, indice).OfType<Line>().MaxBy(l => l.Length)!;
        XYZ p = recta.GetEndPoint(0);
        Punto local = Geometria.Girar([new Punto(LectorZapatas.Mm(p.X), LectorZapatas.Mm(p.Y))], -z.Angulo)[0];
        return c.Direccion == Direccion.X ? local.Y : local.X;
    }

    /// <summary>Barras visibles a través del concreto en la vista activa, para no poner transparencia a mano como en la demostración.</summary>
    private static void Mostrar(Rebar r, View vista)
    {
        if (vista.IsTemplate || vista is not (View3D or ViewPlan or ViewSection))
            return;
        r.SetUnobscuredInView(vista, true);
    }

    /// <summary>La horizontal perpendicular a la barra: la barra queda en un plano vertical y las patas suben o bajan.</summary>
    private static XYZ Normal(ZapataLeida z, ConjuntoBarras c)
    {
        Punto p = Geometria.Girar([c.Direccion == Direccion.X ? new Punto(0, 1) : new Punto(1, 0)], z.Angulo)[0];
        return new XYZ(p.X, p.Y, 0).Normalize();
    }

    /// <summary>De coordenadas de la barra (a lo largo, transversal y altura sobre la base) a puntos del modelo.</summary>
    private static XYZ Punto(ZapataLeida z, ConjuntoBarras c, double largo, double transversal, double altura)
    {
        Punto p = Geometria.Girar([c.Direccion == Direccion.X ? new Punto(largo, transversal) : new Punto(transversal, largo)], z.Angulo)[0];
        return new XYZ(LectorZapatas.Pies(p.X), LectorZapatas.Pies(p.Y), LectorZapatas.Pies(z.ZBase + altura));
    }

    /// <summary>Quita las advertencias; un error deshace toda la generación y su texto va al tablero.</summary>
    private sealed class ManejoFallas : IFailuresPreprocessor
    {
        public List<string> Errores { get; } = [];

        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            bool error = false;
            foreach (FailureMessageAccessor m in a.GetFailureMessages())
            {
                if (m.GetSeverity() == FailureSeverity.Warning)
                {
                    a.DeleteWarning(m);
                    continue;
                }
                error = true;
                Errores.Add(m.GetDescriptionText());
            }
            return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
