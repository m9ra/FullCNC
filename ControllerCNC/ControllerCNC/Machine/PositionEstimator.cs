using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    class PositionEstimator
    {
        /// <summary>
        /// Instruction which is currently processed
        /// </summary>
        StepInstrution _currentInstruction;

        /// <summary>
        /// Current timing of steps.
        /// </summary>
        private int[] _stepTimings = new int[0];

        /// <summary>
        /// Current index into timing.
        /// </summary>
        private int _timingIndex;

        /// <summary>
        /// Currently estimated timing sum.
        /// </summary>
        private long _timingSum;

        /// <summary>
        /// Determine whether estimation ended.
        /// </summary>
        internal bool IsEstimationEnd { get { return _stepTimings.Length == _timingIndex + 1; } }

        /// <summary>
        /// Registers current instruction for estimation.
        /// </summary>
        internal void RegisterInstruction(StepInstrution instruction)
        {
            if (_currentInstruction == instruction)
                //there is nothing to do
                return;

            _currentInstruction = instruction;
            _timingIndex = 0;
            _timingSum = 0;
            _stepTimings = new int[0];
            if (_currentInstruction == null)
                //null instruction - reset buffers only
                return;

            _stepTimings = instruction.GetStepTimings();
        }

        /// <summary>
        /// Gets number of steps which will be done until given time (in microseconds).
        /// Steps are counted from last call.
        /// </summary>
        internal int GetSteps(long targetTime)
        {
            var stepCount = 0;
            while (_timingIndex < _stepTimings.Length)
            {
                var currentTime = _stepTimings[_timingIndex];
                var step = currentTime > 0 ? 1 : -1;
                currentTime = Math.Abs(currentTime);
                if (_timingSum + currentTime > targetTime)
                    break;

                _timingSum += currentTime;
                stepCount += step;
                _timingIndex += 1;
            }
            return stepCount;
        }
    }
}
