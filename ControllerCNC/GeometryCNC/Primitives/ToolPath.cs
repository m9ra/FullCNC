using GeometryCNC.GCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace GeometryCNC.Primitives
{
    [Serializable]
    public class ToolPath
    {
        private readonly List<ToolPathSegment> _segments = new List<ToolPathSegment>();

        public IEnumerable<ToolPathSegment> Targets => _segments;

        public void AddLine(Point3D end, MachineState state)
        {
            var start = _segments.Count == 0 ? new Point3D(0, 0, 0) : _segments.Last().End;
            _segments.Add(new ToolPathSegment(start, end, state.MotionMode));
        }
    }
}
