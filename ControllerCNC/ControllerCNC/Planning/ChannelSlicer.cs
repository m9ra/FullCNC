
using ControllerCNC.Machine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    class ChannelSlicer
    {
        private readonly double _totalLength;

        private readonly double _ratio;

        private readonly int _totalSteps;

        private double _totalLengthAccumulator = 0;

        /// <summary>
        /// How many ticks longer previous instruction was than expected.
        /// </summary>
        private int _slack;

        private double _timeAccumulator = 0.0;

        private int _lastStepCount = 0;

        private ulong _lastInstructionDuration;

        internal bool IsComplete => _totalLengthAccumulator == _totalLength;

        internal double Position => _totalLengthAccumulator / _totalLength;

        internal ChannelSlicer(double totalLength, int totalSteps, double ratio)
        {
            _totalLength = totalLength;
            _totalSteps = Math.Abs(totalSteps);
            _ratio = ratio;
        }

        internal StepInstrution Slice(double totalSpeed, double timeGrain)
        {
            // update total length progress
            var desiredTotalLengthDelta = totalSpeed * timeGrain;
            var realTotalLengthLengthDelta = Math.Min(desiredTotalLengthDelta, _totalLength - _totalLengthAccumulator);
            timeGrain = realTotalLengthLengthDelta / totalSpeed;

            var newLength = _totalLengthAccumulator + realTotalLengthLengthDelta;
            var oldPercentage = _totalLengthAccumulator / _totalLength;
            var newPercentage = newLength / _totalLength;
            _totalLengthAccumulator = newLength;

            // calculate channel specific stuff
            var speed = Math.Abs(totalSpeed * _ratio);

            var oldSteps = oldPercentage * _totalSteps;
            var newSteps = newPercentage * _totalSteps;
            var steps = newSteps - oldSteps;
            var stepCount = Math.Max(0, (int)(Math.Floor(newSteps) - Math.Floor(oldSteps)));

            var totalTickTime = Configuration.TimerFrequency * timeGrain;
            var stepDuration = totalTickTime / steps;
            var offset = distanceToNextStep(oldSteps);
            var offsetTime = offset * stepDuration - stepDuration; //stepDuration can be used as speed because offset/post are step percents

            var instruction = createInstruction(offsetTime + _timeAccumulator - _slack, stepDuration, stepCount);
            _timeAccumulator += totalTickTime;
            return instruction;
        }

        private double distanceToNextStep(double oldSteps)
        {
            var distance = Math.Ceiling(oldSteps) - oldSteps;
            if (distance == 0)
                distance = 1.0;

            return distance;
        }

        private StepInstrution createInstruction(double offsetTime, double stepDuration, int stepCount)
        {
            Debug.WriteLine($"Cp({offsetTime:0.00},{stepDuration:0.00},{stepCount})");

            _lastStepCount = stepCount;
            if (stepCount == 0)
            {
                _lastInstructionDuration = 0;
                return new ConstantInstruction(0, 0, 0);
            }

            checked
            {
                var offset = (int)Math.Round(offsetTime);
                var baseDeltaT = (int)Math.Truncate(stepDuration);
                var remainder = (int)Math.Truncate(stepDuration * stepCount - baseDeltaT * stepCount);

                var instruction = new ConstantInstruction((short)(Math.Sign(_ratio) * stepCount), baseDeltaT, (ushort)remainder, offset);
                _lastInstructionDuration = instruction.GetInstructionDuration();
                return instruction;
            }
        }

        internal void ReportSlack(ulong realDuration)
        {
            checked
            {
                _timeAccumulator -= realDuration;
                if (_timeAccumulator < -1)
                    throw new InvalidOperationException("time counting");
            }
        }
    }
}
