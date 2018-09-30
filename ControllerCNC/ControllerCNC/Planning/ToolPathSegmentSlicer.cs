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

        private readonly double _ratioX, _ratioY, _ratioZ;

        private double _tickAccX, _tickAccY, _tickAccZ;

        private int _currX, _currY, _currZ;

        private ulong _tX, _tY, _tZ;

        internal ToolPathSegmentSlicer(ToolPathSegment segment)
        {
            toSteps(segment.Start, out var sX, out var sY, out var sZ);
            toSteps(segment.End, out var eX, out var eY, out var eZ);

            _totalLength = (segment.End - segment.Start).Length;

            _totalX = eX - sX;
            _totalY = eY - sY;
            _totalZ = eZ - sZ;

            _ratioX = _totalX * Configuration.MilimetersPerStep / _totalLength;
            _ratioY = _totalY * Configuration.MilimetersPerStep / _totalLength;
            _ratioZ = _totalZ * Configuration.MilimetersPerStep / _totalLength;
        }

        internal InstructionCNC Slice(double speed, double desiredTimeGrain)
        {
            var desiredLength = speed * desiredTimeGrain;
            var realLength = Math.Min(desiredLength, _totalLength - _lengthAccumulator);

            var newLength = _lengthAccumulator + realLength;
            var exactTicks = (newLength / speed) * Configuration.TimerFrequency;

            var xInstr = constantInstruction(speed, newLength, exactTicks, _ratioX, _totalX, ref _currX, ref _tickAccX);
            var yInstr = constantInstruction(speed, newLength, exactTicks, _ratioY, _totalY, ref _currY, ref _tickAccY);
            var zInstr = constantInstruction(speed, newLength, exactTicks, _ratioZ, _totalZ, ref _currZ, ref _tickAccZ);

            _tX += xInstr.GetInstructionDuration();
            _tY += yInstr.GetInstructionDuration();
            _tZ += zInstr.GetInstructionDuration();

            _lengthAccumulator = newLength;
            if (IsComplete && (_currX != _totalX || _currY != _totalY || _currZ != _totalZ))
                throw new NotImplementedException("Invalid step counting");

            if (IsComplete && (_tX != _tY || _tY != _tZ))
                throw new NotImplementedException("Invalid tick counting");

            return PlanBuilder3D.Combine(xInstr, yInstr, zInstr);
        }

        private ConstantInstruction constantInstruction(double speed, double newLength, double exactTicks, double ratio, int totalSteps, ref int currSteps, ref double tickAcc)
        {
            var oldPercentage = _lengthAccumulator / _totalLength;
            var newPercentage = newLength / _totalLength;
            var exactSpeed = speed * Math.Abs(ratio);

            var absTotalSteps = Math.Abs(totalSteps);
            var oldSteps = (int)Math.Floor(oldPercentage * absTotalSteps);
            var newSteps = (int)Math.Floor(newPercentage * absTotalSteps);

            var acX = newSteps - oldSteps;
            var exactStepSpeed = exactSpeed / Configuration.MilimetersPerStep;
            var exactStepDuration = Configuration.TimerFrequency / exactStepSpeed;
            var exactTickRemainder = exactStepDuration - Math.Truncate(exactStepDuration);

            var cX = Math.Sign(ratio) * acX;
            currSteps += cX;
            checked
            {
                if (cX == 0)
                {
                    var totalDuration = (int)Math.Truncate(exactTicks);
                    tickAcc += exactTicks - totalDuration;
                    var totalRemainder = (int)Math.Truncate(tickAcc);
                    tickAcc -= totalRemainder;
                    totalDuration += totalRemainder;
                    return new ConstantInstruction(0, totalDuration, 0);
                }

                tickAcc += acX * exactTickRemainder;
                var baseDeltaT = Math.Truncate(exactStepDuration);
                var remainder = Math.Truncate(tickAcc);
                tickAcc -= remainder;
                var instruction = new ConstantInstruction((short)cX, (int)baseDeltaT, (ushort)remainder);

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
