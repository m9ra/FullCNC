using GeometryCNC.GCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace GeometryCNC.Primitives
{
    public class ToolPath
    {
        private readonly List<ToolPathSegment> _segments = new List<ToolPathSegment>();

        public IEnumerable<ToolPathSegment> Targets => _segments;

        public void AddLine(Point3D start, MachineState state)
        {
            _segments.Add(new ToolPathSegment(start,state.MotionMode));
        }
    }
}
