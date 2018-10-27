using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ControllerCNC.Planning
{
    public class ToolPathSegmentSlicer
    {
        internal bool IsComplete => _x.IsComplete;

        public double Position => _x.Position;

        private readonly int _totalX;

        private readonly int _totalY;

        private readonly int _totalZ;

        private readonly double _totalLength;

        private readonly ChannelSlicer _x, _y, _z;

        public ToolPathSegmentSlicer(ToolPathSegment segment)
        {
            toSteps(segment.Start, out var sX, out var sY, out var sZ);
            toSteps(segment.End, out var eX, out var eY, out var eZ);

            var segmentV = segment.End - segment.Start;

            _totalLength = segmentV.Length;
            var se = segment.End;
            var ss = segment.Start;

            _totalX = eX - sX;
            _totalY = eY - sY;
            _totalZ = eZ - sZ;

            var ratio = segmentV;
            ratio.Normalize();

            _x = new ChannelSlicer(_totalLength, _totalX, ratio.X);
            _y = new ChannelSlicer(_totalLength, _totalY, ratio.Y);
            _z = new ChannelSlicer(_totalLength, _totalZ, ratio.Z);
        }

        public InstructionCNC Slice(double speed, double desiredTimeGrain)
        {
            var xInstr = _x.Slice(speed, desiredTimeGrain);
            var yInstr = _y.Slice(speed, desiredTimeGrain);
            var zInstr = _z.Slice(speed, desiredTimeGrain);

            var xd = xInstr.GetInstructionDuration();
            var yd = yInstr.GetInstructionDuration();
            var zd = zInstr.GetInstructionDuration();

            var maxDuration = Math.Max(xd, Math.Max(yd, zd));
            _x.ReportSlack(maxDuration);
            _y.ReportSlack(maxDuration);
            _z.ReportSlack(maxDuration);

            if (_x.IsComplete != _y.IsComplete || _y.IsComplete != _z.IsComplete)
                throw new NotImplementedException("Invalid step counting");

            return PlanBuilder3D.Combine(xInstr, yInstr, zInstr);
        }

        private void toSteps(Point3D p, out int x, out int y, out int z)
        {
            var pmm = new Point3Dmm(p.X, p.Y, p.Z);

            var pStep = pmm.As3Dstep();
            x = pStep.X;
            y = pStep.Y;
            z = pStep.Z;
        }
    }
}
