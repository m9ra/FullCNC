using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public enum InstructionOrientation { Normal = 1, Reversed = -1 };

    public abstract class StepInstrution : InstructionCNC
    {
        /// <summary>
        /// Gets count of steps made by execution of the instruction.
        /// </summary>
        internal readonly Int16 StepCount;

        /// <summary>
        /// Steps that machine really do, considering the orientation.
        /// </summary>
        internal Int16 HwStepCount => (Int16)(StepCount * (int)Orientation);

        /// <summary>
        /// Orientation of the instruction.
        /// </summary>
        internal readonly InstructionOrientation Orientation;

        /// <summary>
        /// Gets timing of all the steps.
        /// </summary>
        internal abstract int[] GetStepTimings();

        /// <summary>
        /// Gets duration of the whole instruction in ticks.
        /// </summary>
        internal abstract ulong GetInstructionDuration();

        /// </inheritdoc>
        internal override bool IsEmpty => StepCount == 0;

        internal abstract bool IsActivationBoundary { get; }

        /// <summary>
        /// Reorganizes instruction according to given orientation if needed.
        /// Internal orientation info is kept, therefore, applying same orientation more than once won't change anything.
        /// </summary>
        /// <param name="orientation">The provided direction. When instrution is created, direction is Normal.</param>
        /// <returns>Instruction with desired orientation.</returns>
        internal abstract StepInstrution WithOrientation(InstructionOrientation orientation);

        protected StepInstrution(Int16 stepCount, InstructionOrientation orientation)
        {
            StepCount = stepCount;
            Orientation = orientation;
        }
    }
}
