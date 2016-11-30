using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{
    class EditorShapePart
    {
        internal IEnumerable<Point2Dmm> Points { get; private set; }

        internal EditorShapePart(IEnumerable<Point2Dmm> points)
        {
            Points = points.ToArray();
        }
    }
}
