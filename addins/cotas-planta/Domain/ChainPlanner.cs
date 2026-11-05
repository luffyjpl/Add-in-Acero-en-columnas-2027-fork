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
    public sealed record ChainSpec(PlanSide Side, ChainKind Kind, int Ring, IReadOnlyList<ChainRef> Refs)
    {
        public Axis2 Along => SideInfo.Along(Side);

        public int SegmentCount => Refs.Count - 1;
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
                int ring = 0;
                foreach (ChainKind kind in WithJoists(layout.Chains, settings))
                {
                    IReadOnlyList<ChainSpec> specs = kind switch
                    {
                        ChainKind.EjeAEje => Single(layout.Side, kind, ring, AxisRefs(geometry, SideInfo.Along(layout.Side))),
                        ChainKind.Libres => Single(layout.Side, kind, ring, FreeRefs(geometry, layout.Side, settings.OnAxisToleranceM)),
                        ChainKind.Voladizo => CantileverSpecs(geometry, layout.Side, ring, settings),
                        ChainKind.Total => Single(layout.Side, kind, ring, TotalRefs(geometry, SideInfo.Along(layout.Side))),
                        ChainKind.Viguetas => Single(layout.Side, kind, ring, JoistRefs(geometry, SideInfo.Along(layout.Side))),
                        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
                    };

                    chains.AddRange(specs);
                    ring = Math.Max(ring, SideInfo.RingCount(specs, layout.Side));
                }
            }

            return chains;
        }

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

        private static IReadOnlyList<ChainSpec> Single(PlanSide side, ChainKind kind, int ring, IReadOnlyList<ChainRef> refs) =>
            refs.Count >= 2 ? new[] { new ChainSpec(side, kind, ring, refs) } : Array.Empty<ChainSpec>();

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
        /// Voladizos: una cota por cada borde que vuela más allá del primer o último eje, siempre medida
        /// desde el eje y cada una en su propia línea (la más corta, más cerca de la planta).
        /// Se miran solo los elementos de la franja de ese lado (entre el eje de borde y el siguiente).
        /// </summary>
        private static IReadOnlyList<ChainSpec> CantileverSpecs(PlanGeometry geometry, PlanSide side, int firstRing, CotasSettings settings)
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
                for (int i = 0; i < ends.Count; i++)
                {
                    ChainRef[] refs = new[] { axisRef, ends[i] }.OrderBy(r => r.Position).ToArray();
                    specs.Add(new ChainSpec(side, ChainKind.Voladizo, firstRing + i, refs));
                }
            }

            return specs;
        }

        /// <summary>Bordes más allá del eje en la dirección <paramref name="direction"/>, del más cercano al más lejano.</summary>
        private static List<ChainRef> CantileverEnds(
            GridAxis axis, int direction, Axis2 along, IReadOnlyList<SolidBox> solids, IEnumerable<BeamSegment> crossBeams, double minLength)
        {
            double Beyond(double position) => (position - axis.Position) * direction;

            var candidates = crossBeams.SelectMany(b => new[]
            {
                new ChainRef(b.Box.Min(along), RefKind.ElementFace, b.Id, AtMin: true),
                new ChainRef(b.Box.Max(along), RefKind.ElementFace, b.Id, AtMin: false),
            }).ToList();

            if (solids.Count > 0)
            {
                SolidBox extreme = direction < 0 ? solids.MinBy(s => s.Box.Min(along))! : solids.MaxBy(s => s.Box.Max(along))!;
                candidates.Add(direction < 0
                    ? new ChainRef(extreme.Box.Min(along), RefKind.ElementFace, extreme.Id, AtMin: true)
                    : new ChainRef(extreme.Box.Max(along), RefKind.ElementFace, extreme.Id, AtMin: false));
            }

            return Distinct(candidates.Where(r => Beyond(r.Position) >= minLength))
                .OrderBy(r => Beyond(r.Position))
                .ToList();
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
