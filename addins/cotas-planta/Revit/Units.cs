using CQEING.CotasPlanta.Domain;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>Revit trabaja en pies; el dominio del add-in en metros.</summary>
    internal static class Units
    {
        public static double ToMeters(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);

        public static double ToFeet(double meters) => UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);

        public static Point2 ToPoint2(XYZ point) => new(ToMeters(point.X), ToMeters(point.Y));
    }
}
