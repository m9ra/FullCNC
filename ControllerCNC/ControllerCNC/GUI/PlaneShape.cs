using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    public class PlaneShape
    {
        /// <summary>
        /// Points belonging into the plane.
        /// </summary>
        public IEnumerable<Point2Dmm> Shape { get; private set; }

        /// <summary>
        /// MInimum of the plane points for first dimension.
        /// </summary>
        public readonly double MinC1;

        /// <summary>
        /// Minimum of the plane points for second dimension.
        /// </summary>
        public readonly double MinC2;

        /// <summary>
        /// Maximum of the plane points for first dimension.
        /// </summary>
        public readonly double MaxC1;

        /// <summary>
        /// Maximum of the plane points for second dimension.
        /// </summary>
        public readonly double MaxC2;

        public PlaneShape(IEnumerable<Point2Dmm> planePoints)
        {
            Shape = planePoints.ToArray();

            MaxC1 = int.MinValue;
            MaxC2 = int.MinValue;

            MinC1 = int.MaxValue;
            MinC2 = int.MaxValue;
            foreach (var point in Shape)
            {
                MaxC1 = Math.Max(point.C1, MaxC1);
                MaxC2 = Math.Max(point.C2, MaxC2);

                MinC1 = Math.Min(point.C1, MinC1);
                MinC2 = Math.Min(point.C2, MinC2);
            }
        }
    }
}
