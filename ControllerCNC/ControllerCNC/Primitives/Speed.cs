using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    /// <summary>
    /// Speed is defined as number of steps that has to be made in specified time.
    /// </summary>
    public class Speed
    {
        /// <summary>
        /// Velocity corresponding to no movement.
        /// </summary>
        public static Speed Zero = new Speed(0, 0);

        /// <summary>
        /// Number of steps that should be done in given time.
        /// </summary>
        public readonly uint StepCount;

        /// <summary>
        /// Time available for doing steps in CNC timer ticks.
        /// </summary>
        public readonly uint Ticks;

        public Speed(uint stepCount, uint ticks)
        {
            StepCount = stepCount;
            Ticks = ticks;
        }

        /// <summary>
        /// Creates a speed corresponding to given deltaT.
        /// </summary>
        /// <param name="deltaT">The delta T in microseconds.</param>
        /// <returns>The speed.</returns>
        public static Speed FromDelta(int deltaT)
        {
            if (deltaT < 0)
                throw new NotSupportedException("DeltaT has to be positive");

            return new Speed(1, (uint)deltaT);
        }
    }
}
