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

        internal abstract int[] GetStepTimings();

        protected StepInstrution(Int16 stepCount)
        {
            StepCount = stepCount;
        }
    }
}
