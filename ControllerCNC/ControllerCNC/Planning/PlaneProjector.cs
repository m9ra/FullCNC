using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media.Media3D;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    /// <summary>
    /// Projects 4D shape of given width to UV, XY.
    /// Assumes the shape will be centered between the planes.
    /// </summary>
    class PlaneProjector
    {
        /// <summary>
        /// Thickness of the shape in mm.
        /// </summary>
        private readonly double _shapeMetricThickness;

        private readonly double _wireLength;

        internal PlaneProjector(double shapeMetricThickness, double wireLength)
        {
            _shapeMetricThickness = shapeMetricThickness;
            _wireLength = wireLength;
        }

        internal IEnumerable<Primitives.Point4Dstep> Project(IEnumerable<Primitives.Point4Dstep> shape)
        {
            var result = new List<Primitives.Point4Dstep>();
            var shapeToTowerDistance = (_shapeMetricThickness - _wireLength) / 2;


            foreach (var point in shape)
            {
                var uvPoint = new Vector3D(point.U, point.V, -_shapeMetricThickness / 2);
                var xyPoint = new Vector3D(point.X, point.Y, +_shapeMetricThickness / 2);

                var projectionVector = uvPoint - xyPoint;
                var projectionVectorScale = shapeToTowerDistance / projectionVector.Z;
                var uvPointProjected = uvPoint + projectionVector * projectionVectorScale;
                var xyPointProjected = xyPoint - projectionVector * projectionVectorScale;

                var projectedPoint = point4D(uvPointProjected.X, uvPointProjected.Y, xyPointProjected.X, xyPointProjected.Y);
                result.Add(projectedPoint);
            }

            return result;
        }

        internal static Point4Dmm Project(Point4Dmm point, double shapeMetricThickness, double wireLength)
        {
            var shapeToTowerDistance = (shapeMetricThickness - wireLength) / 2;

            var uvPoint = new Vector3D(point.U, point.V, -shapeMetricThickness / 2);
            var xyPoint = new Vector3D(point.X, point.Y, +shapeMetricThickness / 2);

            var projectionVector = uvPoint - xyPoint;
            var projectionVectorScale = shapeToTowerDistance / projectionVector.Z;
            var uvPointProjected = uvPoint + projectionVector * projectionVectorScale;
            var xyPointProjected = xyPoint - projectionVector * projectionVectorScale;
            return new Point4Dmm(uvPointProjected.X, uvPointProjected.Y, xyPointProjected.X, xyPointProjected.Y);
        }

        private Primitives.Point4Dstep point4D(double u, double v, double x, double y)
        {
            return new Primitives.Point4Dstep((int)Math.Round(u), (int)Math.Round(v), (int)Math.Round(x), (int)Math.Round(y));
        }
    }
}
