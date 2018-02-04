using ControllerCNC.Machine;
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
    [Serializable]
    public class Speed
    {
        /// <summary>
        /// Velocity corresponding to no movement.
        /// </summary>
        public static Speed Zero = new Speed(0, 1);

        /// <summary>
        /// Number of steps that should be done in given time.
        /// </summary>
        public readonly long StepCount;

        /// <summary>
        /// Time available for doing steps in CNC timer ticks.
        /// </summary>
        public readonly long Ticks;

        public Speed(long stepCount, long ticks)
        {
            StepCount = stepCount;
            Ticks = ticks;
        }

        /// <summary>
        /// Creates a speed corresponding to given deltaT.
        /// </summary>
        /// <param name="deltaT">The delta T in microseconds.</param>
        /// <returns>The speed.</returns>
        public static Speed FromDeltaT(int deltaT)
        {
            if (deltaT < 0)
                throw new NotSupportedException("DeltaT has to be positive");
            if (deltaT == 0)
                //infinity speed is not supported
                return Speed.Zero;

            return new Speed(1, (uint)deltaT);
        }

        public static Speed FromMilimetersPerSecond(double mmPerSecond)
        {
            return new Speed((int)Math.Round(mmPerSecond / Constants.MilimetersPerStep), Constants.TimerFrequency);
        }

        /// <summary>
        /// Converts speed to deltaT.
        /// </summary>
        public int ToDeltaT()
        {
            return (int)Math.Round(1.0 * Ticks / StepCount);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ToDeltaT().ToString();
        }
    }
}
