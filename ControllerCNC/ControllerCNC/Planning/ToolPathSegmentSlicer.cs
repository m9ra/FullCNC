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
    internal class ToolPathSegmentSlicer
    {
        internal bool IsComplete => Position >= 1.0;

        public double Position => _lengthAccumulator / _totalLength;

        private readonly int _totalX;

        private readonly int _totalY;

        private readonly int _totalZ;

        private readonly double _totalLength;

        private double _lengthAccumulator;

        private int _remX, _remY, _remZ;

        private double _accX, _accY, _accZ;

        internal ToolPathSegmentSlicer(ToolPathSegment segment)
        {
            toSteps(segment.Start, out var sX, out var sY, out var sZ);
            toSteps(segment.End, out var eX, out var eY, out var eZ);

            _totalLength = (segment.End - segment.Start).Length;

            _totalX = eX - sX;
            _totalY = eY - sY;
            _totalZ = eZ - sZ;

            _remX = _totalX;
            _remY = _totalY;
            _remZ = _totalZ;
        }

        internal InstructionCNC Slice(double speed, double timeGrain)
        {
            var desiredLength = speed * timeGrain;
            desiredLength = Math.Min(desiredLength, _totalLength - _lengthAccumulator);
            var time = desiredLength / speed;

            var proportion = desiredLength / _totalLength;

            _accX += _totalX * proportion;
            _accY += _totalY * proportion;
            _accZ += _totalZ * proportion;

            var cX = collectSteps(ref _accX);
            var cY = collectSteps(ref _accY);
            var cZ = collectSteps(ref _accZ);

            _remX -= cX;
            _remY -= cY;
            _remZ -= cZ;

            _lengthAccumulator += desiredLength;
            if (IsComplete && (_remX != 0 || _remY != 0 || _remZ != 0))
                throw new NotImplementedException("Invalid step counting");

            var tickCount = time * Configuration.TimerFrequency;

            var xInstr = constantInstruction(tickCount, cX);
            var yInstr = constantInstruction(tickCount, cY);
            var zInstr = constantInstruction(tickCount, cZ);

            return PlanBuilder3D.Combine(xInstr, yInstr, zInstr);
        }

        private ConstantInstruction constantInstruction(double tickCount, int cX)
        {
            checked
            {
                if (cX == 0)
                {
                    return new ConstantInstruction(0, (int)Math.Round(tickCount), 0);
                }

                var exactTickCount = (int)Math.Round(tickCount);

                var acX = Math.Abs(cX);
                var baseDeltaT = exactTickCount / acX;
                var remainder = exactTickCount - baseDeltaT * acX;
                var instruction = new ConstantInstruction((short)cX, baseDeltaT, (ushort)remainder);
                //var realDuration = instruction.GetInstructionDuration();
                var steps = instruction.GetStepTimings();
                var realRealDuration = steps.Sum();
                if (Math.Abs(realRealDuration) != exactTickCount)
                    throw new NotImplementedException();
                return instruction;
            }
        }

        private int collectSteps(ref double acc)
        {
            var result = (int)Math.Round(acc);
            acc -= result;
            return result;
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
