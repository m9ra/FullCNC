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

        private double _slackX, _slackY, _slackZ;

        private int _currX, _currY, _currZ;

        private ulong _tx, _ty, _tz;

        internal ToolPathSegmentSlicer(ToolPathSegment segment)
        {
            toSteps(segment.Start, out var sX, out var sY, out var sZ);
            toSteps(segment.End, out var eX, out var eY, out var eZ);

            _totalLength = (segment.End - segment.Start).Length;
            var se = segment.End;
            var ss = segment.Start;

            _totalX = eX - sX;
            _totalY = eY - sY;
            _totalZ = eZ - sZ;

            var totalAbs = Math.Abs(_totalX) + Math.Abs(_totalY) + Math.Abs(_totalZ);

            var dx = se.X - ss.X;
            var dy = se.Y - ss.Y;
            var dz = se.Z - ss.Z;

            var v = new Vector3D(_totalX, _totalY, _totalZ);
            _ratioX = 1.0 * _totalX / v.Length;
            _ratioY = 1.0 * _totalY / v.Length;
            _ratioZ = 1.0 * _totalZ / v.Length;
        }

        internal InstructionCNC Slice(double speed, double desiredTimeGrain)
        {
            var desiredLength = speed * desiredTimeGrain;
            var realLength = Math.Min(desiredLength, _totalLength - _lengthAccumulator);

            var newLength = _lengthAccumulator + realLength;
            var exactTicks = (realLength / speed) * Configuration.TimerFrequency;

            var xInstr = constantInstruction(speed, newLength, exactTicks, _ratioX, _totalX, ref _currX, ref _tickAccX, ref _slackX);
            var yInstr = constantInstruction(speed, newLength, exactTicks, _ratioY, _totalY, ref _currY, ref _tickAccY, ref _slackY);
            var zInstr = constantInstruction(speed, newLength, exactTicks, _ratioZ, _totalZ, ref _currZ, ref _tickAccZ, ref _slackZ);

            var tx = xInstr.GetInstructionDuration();
            var ty = yInstr.GetInstructionDuration();
            var tz = zInstr.GetInstructionDuration();

            _tx += tx;
            _ty += ty;
            _tz += tz;

            var max = Math.Max(tx, Math.Max(ty, tz));
            _slackX = max - tx;
            _slackY = max - ty;
            _slackZ = max - tz;
            
            _lengthAccumulator = newLength;
            if (IsComplete && (_currX != _totalX || _currY != _totalY || _currZ != _totalZ))
                throw new NotImplementedException("Invalid step counting");

            /*if (IsComplete && (!isTickMatch(_tx, _ty) || !isTickMatch(_ty, _tz) || !isTickMatch(_tz, _tx)))
                throw new NotImplementedException("Invalid tick counting");*/

            return PlanBuilder3D.Combine(xInstr, yInstr, zInstr);
        }

        private bool isTickMatch(ulong t1, ulong t2)
        {
            return t1 == 0 || t2 == 0 || t1 == t2;
        }

        private ConstantInstruction constantInstruction(double speed, double newLength, double exactTicks, double ratio, int totalSteps, ref int currSteps, ref double tickAcc, ref double slack)
        {
            var oldPercentage = _lengthAccumulator / _totalLength;
            var newPercentage = newLength / _totalLength;
            var exactSpeed = speed * Math.Abs(ratio);

            var absTotalSteps = Math.Abs(totalSteps);
            var oldStepsExact = oldPercentage * absTotalSteps;
            var newStepsExact = newPercentage * absTotalSteps;
            var exactSteps = newStepsExact - oldStepsExact;

            var timeToStep = exactTicks / exactSteps;
            var postStepDistance = newStepsExact - Math.Floor(newStepsExact);
            var postTicks = timeToStep * postStepDistance;

            var stepSpeed = exactSpeed / Configuration.MilimetersPerStep;
            var stepDuration = Configuration.TimerFrequency / stepSpeed;
            var allStepTicks = Math.Truncate(exactSteps) * stepDuration;

            var preTicks = exactTicks - postTicks - allStepTicks;
            var totalComposedTime = preTicks + allStepTicks + postTicks;
            if (absTotalSteps != 0 && Math.Abs(totalComposedTime - exactTicks) > 0.0001)
                throw new NotImplementedException();

            var offset = (int)(preTicks - slack - stepDuration);
            var oldSteps = (int)Math.Floor(oldStepsExact);
            var newSteps = (int)Math.Floor(newStepsExact);

            var acX = newSteps - oldSteps;
            var cX = Math.Sign(ratio) * acX;
            currSteps += cX;
            checked
            {
                if (cX == 0)
                {
                    tickAcc = 0;
                    slack = 0;
                    return new ConstantInstruction(0, 0, 0);
                }

                var baseDeltaT = (int)Math.Truncate(stepDuration);
                tickAcc += acX * stepDuration - acX * baseDeltaT;

                if (offset + baseDeltaT < Configuration.MinActivationDelay)
                    offset = Configuration.MinActivationDelay - baseDeltaT;

                var remainder = (int)Math.Truncate(tickAcc);
                tickAcc -= remainder;
                var instruction = new ConstantInstruction((short)cX, baseDeltaT, (ushort)remainder, offset);
                var realStepDuration = instruction.GetInstructionDuration();
                var durationDiff = Math.Abs(realStepDuration + slack - allStepTicks);
                /*if (durationDiff > 1.0)
                    throw new NotImplementedException();*/
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

        private double superCeiling(double number)
        {
            var n = Math.Ceiling(number);
            if (n == number)
                n += 1;

            return n;
        }
    }
}
