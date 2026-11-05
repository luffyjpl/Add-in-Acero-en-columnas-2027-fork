using System;
using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;

namespace CQEING.CotasPlanta.Domain
{
    /// <summary>Qué es cada punto de una cadena: así Revit sabe qué referencia buscar.</summary>
    /// <summary>ElementCenter = el plano central a lo largo del elemento (el eje de una vigueta).</summary>
    public enum RefKind { Axis, ColumnFace, ElementFace, ElementCenter }

    /// <param name="Position">Posición (m) a lo largo de la cadena.</param>
    /// <param name="AtMin">Para caras: true = la cara del lado menor.</param>
    public sealed record ChainRef(double Position, RefKind Kind, long ElementId, bool AtMin = false);

    /// <param name="Ring">0 = la línea más cercana a la estructura. Varias cotas pueden compartir línea.</param>
    /// <param name="TextAt">Dónde va el texto (m, a lo largo de la cadena) cuando no va en el medio.</param>
    /// <param name="TextRow">
    /// Renglón del texto contando desde la línea hacia afuera (0 = pegado a la línea). Cuando dos voladizos
    /// del mismo eje comparten línea, sus textos van lado a lado a la misma altura: el corto (1.35) pegado
    /// a la línea y el largo (2.07) un renglón más afuera. Del lado de la planta estorbarían a la línea vecina.
    /// </param>
    public sealed record ChainSpec(
        PlanSide Side, ChainKind Kind, int Ring, IReadOnlyList<ChainRef> Refs, double? TextAt = null, int TextRow = 0)
    {
        public Axis2 Along => SideInfo.Along(Side);

        public int SegmentCount => Refs.Count - 1;

        /// <summary>Primer punto (m) a lo largo de la cadena.</summary>
        public double From => Refs.Min(r => r.Position);

        /// <summary>Último punto (m) a lo largo de la cadena.</summary>
        public double To => Refs.Max(r => r.Position);
    }

    /// <summary>Geometría de cada lado de la planta.</summary>
    public static class SideInfo
    {
        /// <summary>Dirección en la que corre la cadena.</summary>
        public static Axis2 Along(PlanSide side) => side is PlanSide.Top or PlanSide.Bottom ? Axis2.X : Axis2.Y;

        public static Axis2 Across(PlanSide side) => Along(side).Other();

        /// <summary>+1 si "hacia afuera" es la coordenada creciente (arriba = +Y, derecha = +X).</summary>
        public static int Outward(PlanSide side) => side is PlanSide.Top or PlanSide.Right ? 1 : -1;

        /// <summary>Coordenada (m) de la línea de cota, fuera de la estructura.</summary>
        public static double LinePosition(Rect2 extents, PlanSide side, int ring, CotasSettings settings, int viewScale)
        {
            Axis2 across = Across(side);
            int sign = Outward(side);
            double edge = sign > 0 ? extents.Max(across) : extents.Min(across);
            double offset = CotasSettings.PaperToModel(settings.FirstOffsetMm + ring * settings.SpacingMm, viewScale);
            return edge + sign * offset;
        }

        /// <summary>
        /// Distancia en papel (mm) desde el borde de la estructura hasta el final de las cotas de un lado
        /// (incluye el texto). Más allá van las burbujas de los ejes.
        /// </summary>
        public static double ChainsDepthMm(IEnumerable<ChainSpec> chains, PlanSide side, CotasSettings settings)
        {
            int rings = RingCount(chains, side);
            return rings > 0 ? settings.FirstOffsetMm + (rings - 1) * settings.SpacingMm + settings.TextSizeMm * 1.6 : 0;
        }

        /// <summary>Número de líneas de cota que ocupa un lado.</summary>
        public static int RingCount(IEnumerable<ChainSpec> chains, PlanSide side) =>
            chains.Where(c => c.Side == side).Select(c => c.Ring + 1).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Decide qué cotas van en cada lado y qué puntos mide cada una.
    /// No depende de Revit: el mismo resultado alimenta la vista previa y la creación de cotas.
    /// </summary>
    public static class ChainPlanner
    {
        /// <summary>Dos posiciones a menos de esto (m) se consideran la misma.</summary>
        private const double SamePosition = 0.005;

        public static IReadOnlyList<ChainSpec> Plan(PlanGeometry geometry, CotasSettings settings)
        {
            var chains = new List<ChainSpec>();
            if (geometry.IsEmpty)
            {
                return chains;
            }

            foreach (SideLayout layout in settings.Sides)
            {
                var side = new List<ChainSpec>();
                foreach (ChainKind kind in WithJoists(layout.Chains, settings))
                {
                    foreach (ChainSpec spec in Candidates(geometry, layout.Side, kind, settings))
                    {
                        side.Add(Place(side, spec));
                    }
                }

                chains.AddRange(PlaceNestedTexts(side));
            }

            // Con colores cada tipo necesita su propia cota (su color); en negro se unen.
            return settings.ColoredChains ? chains : JoinLines(chains);
        }

        /// <summary>Otra cadena de la misma línea que se monta sobre esta (voladizos del mismo eje).</summary>
        private static ChainSpec? NestedPartner(IEnumerable<ChainSpec> chains, ChainSpec chain) =>
            chains.FirstOrDefault(c => c != chain && c.Side == chain.Side && c.Ring == chain.Ring && Overlap(c, chain));

        /// <summary>
        /// En cada pareja de voladizos que comparte línea, el texto del largo (2.07) va un renglón más afuera,
        /// centrado en su propia cota: queda junto al del corto (1.35) sin taparlo.
        /// </summary>
        private static IEnumerable<ChainSpec> PlaceNestedTexts(IReadOnlyList<ChainSpec> side)
        {
            foreach (ChainSpec chain in side)
            {
                yield return NestedPartner(side, chain) is { } partner && chain.To - chain.From > partner.To - partner.From
                    ? chain with { TextRow = 1 }
                    : chain;
            }
        }

        /// <summary>
        /// Las cadenas que siguen una tras otra en la misma línea se vuelven una sola cadena:
        /// en Revit sale una única cota que reutiliza el punto compartido (el eje), no varias cotas pegadas.
        /// La cadena unida conserva el tipo de la primera que se colocó en esa línea.
        /// Los voladizos que comparten línea montados uno sobre otro quedan aparte.
        /// </summary>
        public static IReadOnlyList<ChainSpec> JoinLines(IReadOnlyList<ChainSpec> chains)
        {
            var joined = new List<ChainSpec>();
            foreach (IGrouping<(PlanSide Side, int Ring), ChainSpec> line in chains.GroupBy(c => (c.Side, c.Ring)))
            {
                ChainKind kind = line.First().Kind;
                ILookup<bool, ChainSpec> nested = line.ToLookup(c => NestedPartner(line, c) is not null);
                joined.AddRange(nested[true]);
                ChainSpec? current = null;
                foreach (ChainSpec chain in nested[false].OrderBy(c => c.From))
                {
                    if (current is not null && Math.Abs(chain.From - current.To) <= SamePosition)
                    {
                        current = current with { Kind = kind, Refs = Distinct(current.Refs.Concat(chain.Refs)) };
                        continue;
                    }

                    if (current is not null)
                    {
                        joined.Add(current);
                    }

                    current = chain;
                }

                if (current is not null)
                {
                    joined.Add(current);
                }
            }

            return joined;
        }

        /// <summary>Las cadenas de un tipo en un lado, todavía sin línea asignada.</summary>
        private static IReadOnlyList<ChainSpec> Candidates(PlanGeometry geometry, PlanSide side, ChainKind kind, CotasSettings settings)
        {
            Axis2 along = SideInfo.Along(side);
            return kind switch
            {
                ChainKind.EjeAEje => Single(side, kind, AxisRefs(geometry, along)),
                ChainKind.Libres => Single(side, kind, FreeRefs(geometry, side, settings.OnAxisToleranceM)),
                ChainKind.Voladizo => CantileverSpecs(geometry, side, settings),
                ChainKind.Total => Single(side, kind, TotalRefs(geometry, along)),
                ChainKind.Viguetas => Single(side, kind, JoistRefs(geometry, along)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        /// <summary>
        /// Línea de una cadena: la más cercana a la planta donde no se monta sobre otra (tocarse en la punta
        /// sí vale: la cota sigue en la misma línea y se ahorra espacio en el plano). Un voladizo además
        /// puede compartir línea con otro voladizo del mismo eje. La longitud total siempre va sola en la
        /// última línea.
        /// </summary>
        public static ChainSpec Place(IReadOnlyList<ChainSpec> placed, ChainSpec chain)
        {
            if (chain.Kind == ChainKind.Total)
            {
                return chain with { Ring = placed.Count == 0 ? 0 : placed.Max(c => c.Ring) + 1 };
            }

            for (int ring = 0; ; ring++)
            {
                List<ChainSpec> overlapping = placed.Where(p => p.Ring == ring && Overlap(p, chain)).ToList();
                if (overlapping.Count == 0)
                {
                    return chain with { Ring = ring };
                }

                List<ChainSpec> line = placed.Where(p => p.Ring == ring).ToList();
                if (CanNest(line, overlapping, chain))
                {
                    return chain with { Ring = ring };
                }
            }
        }

        /// <summary>
        /// Dos voladizos que salen del mismo eje caben en una línea: el texto de cada uno va en su propio
        /// tramo. Solo dos (un tercero no tendría tramo propio) y solo en una línea de voladizos: en la de
        /// viguetas o de eje a eje se unen en una sola cota y no puede haber nada encima.
        /// </summary>
        private static bool CanNest(IReadOnlyList<ChainSpec> line, IReadOnlyList<ChainSpec> overlapping, ChainSpec chain) =>
            chain.Kind == ChainKind.Voladizo
            && overlapping.Count == 1
            && overlapping[0] is { Kind: ChainKind.Voladizo } other
            && NestedPartner(line, other) is null
            && line.All(c => c.Kind == ChainKind.Voladizo)
            && other.Refs.Any(a => a.Kind == RefKind.Axis
                && chain.Refs.Any(b => b.Kind == RefKind.Axis && Math.Abs(a.Position - b.Position) <= SamePosition));

        /// <summary>Dos cadenas se montan si comparten más que un punto.</summary>
        private static bool Overlap(ChainSpec a, ChainSpec b) =>
            Math.Min(a.To, b.To) - Math.Max(a.From, b.From) > SamePosition;

        /// <summary>La cadena de viguetas va justo después de las libres, en los lados que las llevan.</summary>
        private static IEnumerable<ChainKind> WithJoists(IEnumerable<ChainKind> chains, CotasSettings settings)
        {
            foreach (ChainKind kind in chains)
            {
                yield return kind;
                if (kind == ChainKind.Libres && settings.IncludeJoists)
                {
                    yield return ChainKind.Viguetas;
                }
            }
        }

        /// <summary>Ejes que cruzan la cadena y, entre ellos, el eje central de cada vigueta.</summary>
        private static IReadOnlyList<ChainRef> JoistRefs(PlanGeometry geometry, Axis2 along)
        {
            IReadOnlyList<GridAxis> crossing = geometry.AxesFixed(along);
            if (crossing.Count < 2)
            {
                return Array.Empty<ChainRef>();
            }

            double first = crossing[0].Position, last = crossing[^1].Position;
            List<ChainRef> joists = geometry.Beams
                .Where(b => b.Role == BeamRole.Vigueta && b.Along == along.Other())
                .Where(b => b.Mid.Get(along) > first && b.Mid.Get(along) < last)
                .Select(b => new ChainRef(b.Mid.Get(along), RefKind.ElementCenter, b.Id))
                .ToList();

            // Sin viguetas no hay cadena (sería igual a la de eje a eje).
            return joists.Count == 0
                ? Array.Empty<ChainRef>()
                : Distinct(crossing.Select(a => new ChainRef(a.Position, RefKind.Axis, a.Id)).Concat(joists));
        }

        private static IReadOnlyList<ChainSpec> Single(PlanSide side, ChainKind kind, IReadOnlyList<ChainRef> refs) =>
            refs.Count >= 2 ? new[] { new ChainSpec(side, kind, 0, refs) } : Array.Empty<ChainSpec>();

        /// <summary>Todos los ejes que cruzan la cadena.</summary>
        private static IReadOnlyList<ChainRef> AxisRefs(PlanGeometry geometry, Axis2 along) =>
            geometry.AxesFixed(along)
                .Select(a => new ChainRef(a.Position, RefKind.Axis, a.Id))
                .ToList();

        /// <summary>Longitud total: del elemento más extremo de un lado al del otro.</summary>
        private static IReadOnlyList<ChainRef> TotalRefs(PlanGeometry geometry, Axis2 along)
        {
            List<SolidBox> solids = geometry.Solids.ToList();
            if (solids.Count == 0)
            {
                return Array.Empty<ChainRef>();
            }

            SolidBox first = solids.MinBy(s => s.Box.Min(along))!;
            SolidBox last = solids.MaxBy(s => s.Box.Max(along))!;
            return new[]
            {
                new ChainRef(first.Box.Min(along), RefKind.ElementFace, first.Id, AtMin: true),
                new ChainRef(last.Box.Max(along), RefKind.ElementFace, last.Id, AtMin: false),
            };
        }

        /// <summary>
        /// Cotas libres (cara a cara): las caras de las columnas sobre el eje de borde
        /// y las caras de las vigas intermedias que cortan la luz entre ejes.
        /// </summary>
        private static IReadOnlyList<ChainRef> FreeRefs(PlanGeometry geometry, PlanSide side, double tolerance)
        {
            Axis2 along = SideInfo.Along(side);
            Axis2 across = along.Other();
            int sign = SideInfo.Outward(side);

            GridAxis? edgeAxis = geometry.AxesFixed(across).OrderBy(a => a.Position * sign).LastOrDefault();
            IReadOnlyList<GridAxis> crossing = geometry.AxesFixed(along);
            if (edgeAxis is null || crossing.Count == 0)
            {
                return Array.Empty<ChainRef>();
            }

            IEnumerable<ChainRef> columnFaces = geometry.Columns
                .Where(c => c.Box.Min(across) - tolerance <= edgeAxis.Position && edgeAxis.Position <= c.Box.Max(across) + tolerance)
                .SelectMany(c => new[]
                {
                    new ChainRef(c.Box.Min(along), RefKind.ColumnFace, c.Id, AtMin: true),
                    new ChainRef(c.Box.Max(along), RefKind.ColumnFace, c.Id, AtMin: false),
                });

            double first = crossing[0].Position, last = crossing[^1].Position;
            IEnumerable<ChainRef> intermediateFaces = geometry.Beams
                .Where(b => b.Role == BeamRole.Viga && b.AxisName is null && b.Along == across)
                .Where(b => b.Box.Min(along) > first && b.Box.Max(along) < last)
                .SelectMany(b => new[]
                {
                    new ChainRef(b.Box.Min(along), RefKind.ElementFace, b.Id, AtMin: true),
                    new ChainRef(b.Box.Max(along), RefKind.ElementFace, b.Id, AtMin: false),
                });

            return Distinct(columnFaces.Concat(intermediateFaces));
        }

        /// <summary>
        /// Voladizos: una cota por cada borde que vuela más allá del primer o último eje, siempre completa
        /// desde el eje (1.45 y 2.07, no 1.45 y 0.62). El orden decide la línea de cada una: la cara
        /// exterior más cercana sigue en la línea de las viguetas o de eje a eje, la siguiente abre una
        /// línea de voladizos y las caras interiores (1.35) comparten esa línea, cada texto en su tramo.
        /// Se miran solo los elementos de la franja de ese lado (entre el eje de borde y el siguiente).
        /// </summary>
        private static IReadOnlyList<ChainSpec> CantileverSpecs(PlanGeometry geometry, PlanSide side, CotasSettings settings)
        {
            Axis2 along = SideInfo.Along(side);
            Axis2 across = along.Other();
            int sign = SideInfo.Outward(side);

            IReadOnlyList<GridAxis> crossing = geometry.AxesFixed(along);
            List<GridAxis> bandAxes = geometry.AxesFixed(across).OrderBy(a => a.Position * sign).ToList();
            if (crossing.Count == 0 || bandAxes.Count == 0)
            {
                return Array.Empty<ChainSpec>();
            }

            double edge = bandAxes[^1].Position;
            double inner = bandAxes.Count > 1 ? bandAxes[^2].Position : edge;
            double tolerance = settings.OnAxisToleranceM;
            // Franja del lado: desde el eje de borde hacia afuera y hasta el eje siguiente hacia adentro.
            // Un elemento entra si se mete en ella más que la tolerancia (tocar el borde no basta).
            double bandMin = sign > 0 ? Math.Min(edge, inner) : double.NegativeInfinity;
            double bandMax = sign > 0 ? double.PositiveInfinity : Math.Max(edge, inner);
            bool InBand(Rect2 box) =>
                Math.Min(box.Max(across), bandMax) - Math.Max(box.Min(across), bandMin) > tolerance;

            List<SolidBox> solids = geometry.Solids.Where(s => InBand(s.Box)).ToList();
            List<BeamSegment> crossBeams = geometry.Beams.Where(b => b.Along == across && InBand(b.Box)).ToList();

            List<ChainRef> low = CantileverEnds(crossing[0], -1, along, solids, crossBeams, settings.CantileverMinM);
            List<ChainRef> high = CantileverEnds(crossing[^1], +1, along, solids, crossBeams, settings.CantileverMinM);

            var specs = new List<ChainSpec>();
            foreach ((GridAxis axis, List<ChainRef> ends) in new[] { (crossing[0], low), (crossing[^1], high) })
            {
                var axisRef = new ChainRef(axis.Position, RefKind.Axis, axis.Id);
                foreach (ChainRef end in ends)
                {
                    specs.Add(new ChainSpec(side, ChainKind.Voladizo, 0, Distinct(new[] { axisRef, end })));
                }
            }

            return specs;
        }

        /// <summary>
        /// Bordes más allá del eje en la dirección <paramref name="direction"/>: primero las caras
        /// exteriores (de las vigas y el extremo de la estructura), del más cercano al más lejano;
        /// después las caras interiores de las vigas (la de 1.35 de una viga de borde de 1.45).
        /// </summary>
        private static List<ChainRef> CantileverEnds(
            GridAxis axis, int direction, Axis2 along, IReadOnlyList<SolidBox> solids, IReadOnlyList<BeamSegment> crossBeams, double minLength)
        {
            double Beyond(double position) => (position - axis.Position) * direction;

            // Cara de una caja hacia afuera (outer) o hacia el eje.
            ChainRef Face(long id, Rect2 box, bool outer) => (direction < 0) == outer
                ? new ChainRef(box.Min(along), RefKind.ElementFace, id, AtMin: true)
                : new ChainRef(box.Max(along), RefKind.ElementFace, id, AtMin: false);

            List<ChainRef> outerFaces = crossBeams.Select(b => Face(b.Id, b.Box, outer: true)).ToList();
            if (solids.Count > 0)
            {
                SolidBox extreme = direction < 0 ? solids.MinBy(s => s.Box.Min(along))! : solids.MaxBy(s => s.Box.Max(along))!;
                outerFaces.Add(Face(extreme.Id, extreme.Box, outer: true));
            }

            IEnumerable<ChainRef> innerFaces = crossBeams.Select(b => Face(b.Id, b.Box, outer: false));

            var ends = new List<ChainRef>();
            foreach (ChainRef end in outerFaces.Where(r => Beyond(r.Position) >= minLength).OrderBy(r => Beyond(r.Position))
                .Concat(innerFaces.Where(r => Beyond(r.Position) >= minLength).OrderBy(r => Beyond(r.Position))))
            {
                if (ends.All(e => Math.Abs(e.Position - end.Position) > SamePosition))
                {
                    ends.Add(end);
                }
            }

            return ends;
        }

        /// <summary>Ordena por posición y quita puntos repetidos.</summary>
        private static List<ChainRef> Distinct(IEnumerable<ChainRef> refs)
        {
            var result = new List<ChainRef>();
            foreach (ChainRef r in refs.OrderBy(r => r.Position))
            {
                if (result.Count == 0 || r.Position - result[^1].Position > SamePosition)
                {
                    result.Add(r);
                }
            }

            return result;
        }
    }
}
