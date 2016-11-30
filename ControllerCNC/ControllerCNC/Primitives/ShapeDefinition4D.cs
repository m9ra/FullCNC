using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class ShapeDefinition4D
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        public readonly IEnumerable<Point4Dmm> Points;

        /// <summary>
        /// Thickness of the shape.
        /// </summary>
        public readonly double Thickness;

        internal ShapeDefinition4D(IEnumerable<Point4Dmm> points, double thickness)
        {
            Points = points.ToArray();
            Thickness = thickness;
        }
    }
}
