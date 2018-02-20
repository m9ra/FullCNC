using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class PlanStream3D
    {
        private readonly PlanPart3D[] _plan;

        private readonly Dictionary<PlanPart3D, double> _rampTimeCache = new Dictionary<PlanPart3D, double>();

        private int _currentIndex = 0;

        private double _currentInstructionOffset = 0;

        internal readonly double TotalRampTime;

        internal readonly double TotalConstantDistance;

        private double _currentConstantDistance = 0.0;

        private double _currentRampTime = 0.0;

        public bool IsComplete { get { return _currentIndex >= _plan.Length; } }

        public PlanStream3D(PlanPart3D[] planPart3D)
        {
            _plan = planPart3D;

            TotalRampTime = calculateRampTime(_plan);
            TotalConstantDistance = calculateConstantDistance(_plan);
        }

        internal IEnumerable<InstructionCNC> ShiftByConstantSpeed(double lengthLimit, Speed speed)
        {
            var part = _plan[_currentIndex];

            var length = part.EndPoint.DistanceTo(part.StartPoint);
            if (length == 0)
            {
                _currentInstructionOffset = 0;
                ++_currentIndex;
                return new InstructionCNC[0];
            }

            var currentLength = Math.Min(length - _currentInstructionOffset, lengthLimit);

            var lastEnd = part.StartPoint.ShiftTo(part.EndPoint, _currentInstructionOffset / length);
            _currentInstructionOffset += currentLength;
            var currentEnd = part.StartPoint.ShiftTo(part.EndPoint, _currentInstructionOffset / length);

            var diff1 = PlanBuilder3D.GetStepDiff(lastEnd, part.EndPoint);
            var diff2 = PlanBuilder3D.GetStepDiff(currentEnd, part.EndPoint);

            if (_currentInstructionOffset >= length)
            {
                _currentInstructionOffset = 0;
                ++_currentIndex;
            }

            var builder = new PlanBuilder();
            builder.AddConstantSpeedTransitionUVXY(diff1.U - diff2.U, diff1.V - diff2.V, speed, diff1.X - diff2.X, diff1.Y - diff2.Y, speed);

            var plan = builder.Build();
            return plan;
        }

        internal double GetRemainingConstantDistance()
        {
            return TotalConstantDistance - _currentConstantDistance;
        }

        internal double GetRemainingRampTime()
        {
            return TotalRampTime - _currentRampTime;
        }

        internal IEnumerable<InstructionCNC> NextRampInstructions()
        {
            if (_currentInstructionOffset != 0)
                throw new NotSupportedException("Cannot generate next instruction when offset is present");

            var part = _plan[_currentIndex];
            var diff = PlanBuilder3D.GetStepDiff(part.StartPoint, part.EndPoint);

            var builder = new PlanBuilder();
            if (part.AccelerationRamp == null)
            {
                builder.AddConstantSpeedTransitionUVXY(diff.U, diff.V, part.SpeedLimit, diff.X, diff.Y, part.SpeedLimit);
                _currentConstantDistance += part.StartPoint.DistanceTo(part.EndPoint);
            }
            else
            {
                builder.AddRampedLineUVXY(diff.U, diff.V, diff.X, diff.Y, part.AccelerationRamp, part.SpeedLimit);
                _currentRampTime += getRampTime(part);
            }

            var plan = builder.Build();
            ++_currentIndex;
            _currentInstructionOffset = 0;
            return plan;
        }

        internal bool IsSpeedLimitedBy(Speed maxCuttingSpeed)
        {
            var currentPart = _plan[_currentIndex];
            return currentPart.SpeedLimit.ToMetric() <= maxCuttingSpeed.ToMetric();
        }

        private double calculateConstantDistance(IEnumerable<PlanPart3D> plan)
        {
            var accumulator = 0.0;
            foreach (var part in plan)
            {
                if (part.AccelerationRamp != null)
                    continue;

                var distance = part.StartPoint.DistanceTo(part.EndPoint);
                accumulator += distance;
            }

            return accumulator;
        }

        private double calculateRampTime(IEnumerable<PlanPart3D> plan)
        {
            var accumulator = 0.0;
            foreach (var part in plan)
            {
                if (part.AccelerationRamp == null)
                    continue;

                accumulator += getRampTime(part);
            }

            return accumulator;
        }

        private double getRampTime(PlanPart3D part)
        {
            if (!_rampTimeCache.TryGetValue(part, out var time))
            {
                var builder = new PlanBuilder();
                var diff = PlanBuilder3D.GetStepDiff(part.StartPoint, part.EndPoint);
                builder.AddRampedLineUVXY(diff.U, diff.V, diff.X, diff.Y, part.AccelerationRamp, part.SpeedLimit);
                var instructions = builder.Build();

                var accumulator = 0.0;
                foreach(var instruction in instructions)
                {
                    var axes = instruction as Axes;
                    if (axes == null)
                        continue;

                    accumulator += axes.CalculateTotalTime() * 1.0 / Configuration.TimerFrequency;
                }
                _rampTimeCache[part] = time = accumulator;
            }

            return time;
        }
    }
}
