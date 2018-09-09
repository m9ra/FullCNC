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
        public readonly Point3D End;

        public readonly Point3D Start;

        public readonly MotionMode MotionMode;

        public double Length => (End - Start).Length;

        public ToolPathSegment(Point3D start, Point3D end, MotionMode motionMode)
        {
            Start = start;
            End = end;
            MotionMode = motionMode;
        }
    }
}
