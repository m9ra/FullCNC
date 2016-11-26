using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{
    class FacetShape
    {
        internal IEnumerable<Point2Df> Points;

        internal FacetShape(IEnumerable<Point2Df> points)
        {
            Points = points.ToArray();
        }
    }
}
