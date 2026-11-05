using System;
using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>
    /// Tipos propios del add-in (prefijo "CQEING "): un tipo de cota por color, un tipo de texto
    /// y un estilo de línea para las X de vacíos. Se crean la primera vez y luego se reutilizan.
    /// </summary>
    internal sealed class AnnotationStyles
    {
        public const string Prefix = "CQEING ";

        private readonly IReadOnlyDictionary<ChainKind, DimensionType> _dimensionTypes;

        private AnnotationStyles(IReadOnlyDictionary<ChainKind, DimensionType> dimensionTypes, ElementId textTypeId, GraphicsStyle voidLine)
        {
            _dimensionTypes = dimensionTypes;
            TextTypeId = textTypeId;
            VoidLine = voidLine;
        }

        public ElementId TextTypeId { get; }

        public GraphicsStyle VoidLine { get; }

        public DimensionType For(ChainKind kind) => _dimensionTypes[kind];

        /// <summary>Debe llamarse dentro de una transacción.</summary>
        public static AnnotationStyles Ensure(Document doc, CotasSettings settings)
        {
            List<DimensionType> linear = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.Linear && !string.IsNullOrWhiteSpace(t.Name))
                .ToList();

            DimensionType baseType = linear.FirstOrDefault(t => t.Name == settings.BaseDimensionTypeName)
                ?? linear.FirstOrDefault(t => !t.Name.StartsWith(Prefix))
                ?? throw new InvalidOperationException("El proyecto no tiene tipos de cota lineal.");

            Dictionary<ChainKind, DimensionType> dimensionTypes = Enum.GetValues<ChainKind>()
                .ToDictionary(kind => kind, kind => EnsureDimensionType(linear, baseType, kind, settings.DimensionColor(kind)));

            return new AnnotationStyles(dimensionTypes, EnsureTextType(doc, settings), EnsureVoidLine(doc));
        }

        /// <summary>
        /// Los tipos son del add-in y se comparten entre vistas: cambiar el color aquí cambia también
        /// las vistas de cotas que ya existen.
        /// </summary>
        private static DimensionType EnsureDimensionType(IEnumerable<DimensionType> existing, DimensionType baseType, ChainKind kind, Rgb color)
        {
            string name = Prefix + Palette.ChainLabel(kind);
            DimensionType type = existing.FirstOrDefault(t => t.Name == name) ?? (DimensionType)baseType.Duplicate(name);
            type.get_Parameter(BuiltInParameter.LINE_COLOR)?.Set(ToRevitInt(color));
            return type;
        }

        private static ElementId EnsureTextType(Document doc, CotasSettings settings)
        {
            const string name = Prefix + "Texto de planta";
            List<TextNoteType> types = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().ToList();

            TextNoteType? type = types.FirstOrDefault(t => t.Name == name);
            if (type is null)
            {
                TextNoteType source = doc.GetElement(doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType)) as TextNoteType
                    ?? types.FirstOrDefault()
                    ?? throw new InvalidOperationException("El proyecto no tiene tipos de texto.");
                type = (TextNoteType)source.Duplicate(name);
            }

            type.get_Parameter(BuiltInParameter.TEXT_SIZE)?.Set(UnitUtils.ConvertToInternalUnits(settings.TextSizeMm, UnitTypeId.Millimeters));
            type.get_Parameter(BuiltInParameter.LINE_COLOR)?.Set(ToRevitInt(Palette.Ink));
            type.get_Parameter(BuiltInParameter.TEXT_BACKGROUND)?.Set(1); // 1 = transparente
            return type.Id;
        }

        private static GraphicsStyle EnsureVoidLine(Document doc)
        {
            const string name = Prefix + "Vacíos";
            Category lines = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            Category style = lines.SubCategories.Contains(name)
                ? lines.SubCategories.get_Item(name)
                : doc.Settings.Categories.NewSubcategory(lines, name);

            style.LineColor = ToRevitColor(Palette.VoidMark);
            style.SetLineWeight(1, GraphicsStyleType.Projection);
            return style.GetGraphicsStyle(GraphicsStyleType.Projection);
        }

        public static Color ToRevitColor(Rgb rgb) => new(rgb.R, rgb.G, rgb.B);

        /// <summary>Revit guarda el color de las cotas como un entero: rojo + verde·256 + azul·65536.</summary>
        private static int ToRevitInt(Rgb rgb) => rgb.R | (rgb.G << 8) | (rgb.B << 16);
    }
}
