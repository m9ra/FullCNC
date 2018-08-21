using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace GeometryCNC.Primitives
{
    [Serializable]
    public class ToolPathSegment
    {
        public readonly Point3D Point;

        public readonly MotionMode MotionMode;

        internal ToolPathSegment(Point3D start, MotionMode motionMode)
        {
            Point = start;
            MotionMode = motionMode;
        }
    }
}
