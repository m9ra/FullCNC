using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Machine
{
    /// <summary>
    /// Encapsulates constants related to the CNC machine. All the configuration
    /// like machine limits and capabilities (not communication related staff) has to be here.
    /// </summary>
    static class Constants
    {
        /// <summary>
        /// Time scale of the machine. (2MHz)
        /// </summary>
        internal static readonly uint TimerFrequency = 2000000;

        /// <summary>
        /// How many steps for single revolution has to be done.
        /// </summary>
        internal static readonly uint StepsPerRevolution = 400;

        /// <summary>
        /// Maximal safe acceleration in steps/s^2.
        /// </summary>
        internal static readonly uint MaxAcceleration = 20 * StepsPerRevolution;

        /// <summary>
        /// DeltaT which can be safely used after stand still.
        /// </summary>
        public static readonly int StartDeltaT = 2000;

        /// <summary>
        /// Fastest DeltaT which is supported
        /// </summary>
        public static int FastestDeltaT = 350;

        /// <summary>
        /// Speed which is safe to turn around without any additional delays.
        /// </summary>
        public static readonly Speed ReverseSafeSpeed = Speed.FromDelta(StartDeltaT);

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Speed MaxPlaneSpeed = Speed.FromDelta(FastestDeltaT);

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Acceleration MaxPlaneAcceleration = new Acceleration(new Speed(MaxAcceleration, TimerFrequency), TimerFrequency);
    }
}
