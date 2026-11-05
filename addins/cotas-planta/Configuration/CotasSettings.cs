using System;
using System.Collections.Generic;

namespace CQEING.CotasPlanta.Configuration
{
    /// <summary>Lado de la planta donde se dibuja una cadena de cotas.</summary>
    public enum PlanSide { Top, Bottom, Left, Right }

    /// <summary>Tipo de cadena. Cada una se dibuja con su propio color.</summary>
    public enum ChainKind
    {
        /// <summary>Cara de columna y luz libre (0.30 / 2.71 / 0.30).</summary>
        Libres,

        /// <summary>Distancia entre ejes (3.01 / 2.99).</summary>
        EjeAEje,

        /// <summary>Desde el eje hasta el extremo del elemento que vuela.</summary>
        Voladizo,

        /// <summary>Longitud total, de extremo a extremo de la estructura.</summary>
        Total,

        /// <summary>Separación de viguetas: eje, eje de cada vigueta, eje. Va justo después de las libres.</summary>
        Viguetas,
    }

    /// <summary>Cómo se rotulan las vigas en planta.</summary>
    public enum BeamNaming
    {
        /// <summary>(0.20 x 0.35) sobre cada viga.</summary>
        Section,

        /// <summary>V1, VA… en planta y el nombre completo en la leyenda.</summary>
        Short,
    }

    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>Cadenas que lleva un lado, ordenadas de adentro hacia afuera.</summary>
    public sealed record SideLayout(PlanSide Side, IReadOnlyList<ChainKind> Chains);

    /// <summary>Colores y textos compartidos por la vista previa y Revit.</summary>
    public static class Palette
    {
        /// <summary>Negro de las cotas: en los planos las cotas y sus textos van en negro.</summary>
        public static readonly Rgb Ink = new(0, 0, 0);
        public static readonly Rgb Column = new(242, 140, 40);
        public static readonly Rgb SlabTint = new(242, 243, 245);
        public static readonly Rgb SlabEdge = new(170, 176, 184);
        public static readonly Rgb VoidMark = new(154, 162, 171);

        public static Rgb ChainColor(ChainKind kind) => kind switch
        {
            ChainKind.Libres => new Rgb(27, 31, 36),
            ChainKind.EjeAEje => new Rgb(11, 100, 180),
            ChainKind.Voladizo => new Rgb(166, 84, 0),
            ChainKind.Total => new Rgb(110, 60, 160),
            ChainKind.Viguetas => new Rgb(0, 128, 110),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ChainColorName(ChainKind kind) => kind switch
        {
            ChainKind.Libres => "Negro",
            ChainKind.EjeAEje => "Azul",
            ChainKind.Voladizo => "Ámbar",
            ChainKind.Total => "Morado",
            ChainKind.Viguetas => "Verde azulado",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ChainLabel(ChainKind kind) => kind switch
        {
            ChainKind.Libres => "Cota libre (cara a cara)",
            ChainKind.EjeAEje => "Cota eje a eje",
            ChainKind.Voladizo => "Voladizo (eje a extremo)",
            ChainKind.Total => "Longitud total",
            ChainKind.Viguetas => "Separación de viguetas",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>Todo lo configurable del add-in en un solo lugar.</summary>
    public sealed record CotasSettings
    {
        /// <summary>Distancia en papel (mm) entre el borde de la estructura y la primera cadena.</summary>
        public double FirstOffsetMm { get; init; } = 12;

        /// <summary>Separación en papel (mm) entre cadenas del mismo lado.</summary>
        public double SpacingMm { get; init; } = 7;

        /// <summary>
        /// Altura de texto en papel (mm) de rótulos y leyenda. No cambia con la escala:
        /// al cambiar la escala crece o se achica el dibujo, el texto se queda igual.
        /// </summary>
        public double TextSizeMm { get; init; } = 2.5;

        /// <summary>Escala de la vista de cotas (el 50 de 1:50).</summary>
        public int ViewScale { get; init; } = 50;

        /// <summary>
        /// Cada tipo de cadena con su color. Apagado, todas van en negro, como se entregan los planos.
        /// En Revit el color del tipo de cota pinta la línea y el texto a la vez.
        /// </summary>
        public bool ColoredChains { get; init; }

        /// <summary>Tolerancia (m) para decidir que un elemento está sobre un eje.</summary>
        public double OnAxisToleranceM { get; init; } = 0.05;

        /// <summary>Longitud mínima (m) más allá del último eje para considerar que hay voladizo.</summary>
        public double CantileverMinM { get; init; } = 0.20;

        public bool MarkVoids { get; init; } = true;

        /// <summary>Línea punteada desde el elemento medido hasta su cota, cuando está lejos de ella.</summary>
        public bool ShowReferenceLines { get; init; } = true;

        public bool TintSlab { get; init; } = true;

        /// <summary>Transparencia (%) de la losa en la vista de cotas.</summary>
        public int SlabTransparency { get; init; } = 80;

        /// <summary>Agregar la cadena de separación de viguetas en los lados que llevan cotas libres.</summary>
        public bool IncludeJoists { get; init; } = true;

        /// <summary>Trazo diagonal más largo en los extremos de cada cota.</summary>
        public bool LongTicks { get; init; } = true;

        public BeamNaming BeamNaming { get; init; } = BeamNaming.Short;

        /// <summary>Tipo de cota lineal que se duplica para crear los tres tipos de color.</summary>
        public string? BaseDimensionTypeName { get; init; }

        public IReadOnlyList<SideLayout> Sides { get; init; } = DefaultSides;

        /// <summary>Distribución de la planta de referencia.</summary>
        public static IReadOnlyList<SideLayout> DefaultSides { get; } = new[]
        {
            new SideLayout(PlanSide.Top, new[] { ChainKind.Libres, ChainKind.EjeAEje, ChainKind.Total }),
            new SideLayout(PlanSide.Left, new[] { ChainKind.Libres, ChainKind.Voladizo, ChainKind.Total }),
            new SideLayout(PlanSide.Right, new[] { ChainKind.EjeAEje }),
            new SideLayout(PlanSide.Bottom, Array.Empty<ChainKind>()),
        };

        /// <summary>Escalas que se ofrecen para la vista de cotas.</summary>
        public static IReadOnlyList<int> ScaleOptions { get; } = new[] { 20, 25, 50, 75, 100, 125, 200 };

        /// <summary>Color con que se dibuja una cadena: el de su tipo o negro.</summary>
        public Rgb DimensionColor(ChainKind kind) => ColoredChains ? Palette.ChainColor(kind) : Palette.Ink;

        /// <summary>Convierte milímetros de papel a metros de modelo según la escala de la vista.</summary>
        public static double PaperToModel(double paperMm, int viewScale) => paperMm * viewScale / 1000.0;
    }
}
