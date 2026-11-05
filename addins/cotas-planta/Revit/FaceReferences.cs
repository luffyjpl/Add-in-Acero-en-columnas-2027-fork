using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQEING.CotasPlanta.Revit
{
    /// <summary>Busca la cara de un elemento que una cota puede usar como referencia.</summary>
    internal static class FaceReferences
    {
        private const double ParallelTolerance = 0.999;

        /// <summary>
        /// Cara plana perpendicular a <paramref name="along"/> en el extremo menor o mayor del elemento.
        /// Si la geometría no da caras, prueba con los planos de referencia de la familia.
        /// </summary>
        public static Reference? Find(Element element, XYZ along, bool atMin, View view)
        {
            var options = new Options { ComputeReferences = true, View = view };
            List<(Reference Reference, double Position)> faces = PlanarFaces(element.get_Geometry(options), Transform.Identity)
                .Where(f => f.Face.Reference is not null && Math.Abs(f.Normal.DotProduct(along)) > ParallelTolerance)
                .Select(f => (f.Face.Reference, f.Origin.DotProduct(along)))
                .ToList();

            if (faces.Count > 0)
            {
                return atMin ? faces.MinBy(f => f.Position).Reference : faces.MaxBy(f => f.Position).Reference;
            }

            return element is FamilyInstance instance ? FromFamilyPlanes(instance, along, atMin) : null;
        }

        /// <summary>Left/Right (o Front/Back) de la familia, según cuál quede perpendicular a la cota.</summary>
        private static Reference? FromFamilyPlanes(FamilyInstance instance, XYZ along, bool atMin)
        {
            double hand = instance.HandOrientation.DotProduct(along);
            if (Math.Abs(hand) > ParallelTolerance)
            {
                bool leftIsMin = hand > 0;
                return First(instance, atMin == leftIsMin ? FamilyInstanceReferenceType.Left : FamilyInstanceReferenceType.Right);
            }

            bool backIsMax = instance.FacingOrientation.DotProduct(along) > 0;
            return First(instance, atMin == backIsMax ? FamilyInstanceReferenceType.Front : FamilyInstanceReferenceType.Back);
        }

        private static Reference? First(FamilyInstance instance, FamilyInstanceReferenceType type) =>
            instance.GetReferences(type).FirstOrDefault();

        /// <summary>
        /// Recorre la geometría. En instancias usa la geometría del símbolo (sus referencias son las que
        /// Revit acepta para cotas) y la transformación para saber la posición real.
        /// </summary>
        private static IEnumerable<(PlanarFace Face, XYZ Normal, XYZ Origin)> PlanarFaces(GeometryElement? geometry, Transform transform)
        {
            if (geometry is null)
            {
                yield break;
            }

            foreach (GeometryObject obj in geometry)
            {
                if (obj is GeometryInstance instance)
                {
                    Transform combined = transform.Multiply(instance.Transform);
                    foreach (var face in PlanarFaces(instance.GetSymbolGeometry(), combined))
                    {
                        yield return face;
                    }
                }
                else if (obj is Solid solid && solid.Volume > 0)
                {
                    foreach (PlanarFace face in solid.Faces.OfType<PlanarFace>())
                    {
                        yield return (face, transform.OfVector(face.FaceNormal), transform.OfPoint(face.Origin));
                    }
                }
            }
        }
    }
}
