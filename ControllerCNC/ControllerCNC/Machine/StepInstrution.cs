using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public abstract class StepInstrution : InstructionCNC
    {
        /// <summary>
        /// Gets count of steps made by execution of the instruction.
        /// </summary>
        internal readonly Int16 StepCount;

        /// <summary>
        /// Gets timing of all the steps.
        /// </summary>
        internal abstract int[] GetStepTimings();

        /// <summary>
        /// Gets duration of the whole instruction in ticks.
        /// </summary>
        internal abstract ulong GetInstructionDuration();

        protected StepInstrution(Int16 stepCount)
        {
            StepCount = stepCount;
        }
    }
}
