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
        /// Thickness of the hotwire [mm].
        /// </summary>
        public static readonly double HotwireThickness = 0.28;

        /// <summary>
        /// Time scale of the machine. (2MHz)
        /// </summary>
        public static readonly uint TimerFrequency = 2000000;

        /// <summary>
        /// How many steps for single revolution has to be done.
        /// </summary>
        public static readonly int StepsPerRevolution = 400;

        /// <summary>
        /// Screw with 1.25mm per revolution.
        /// </summary>
        public static readonly double MilimetersPerStep = 1.25 / StepsPerRevolution;

        /// <summary>
        /// Distance betwee UV XY towers in mm when fully expanded.
        /// </summary>
        public static readonly double FullWireLength = 605;

        /// <summary>
        /// How many steps at maximum is allowed for instruction planning.
        /// </summary>
        public static readonly int MaxStepInstructionLimit = 31500;

        /// <summary>
        /// Maximal safe acceleration in steps/s^2.
        /// </summary>
        public static readonly int MaxAcceleration = 200 * StepsPerRevolution;

        /// <summary>
        /// U axis is 460mm long
        /// </summary>
        public static readonly int MaxStepsU = 460 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// V axis is 256mm long
        /// </summary>
        public static readonly int MaxStepsV = 256 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// X axis is 460mm long
        /// </summary>
        public static readonly int MaxStepsX = 460 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// Y axis is 256mm long
        /// </summary>
        public static readonly int MaxStepsY = 256 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// DeltaT which can be safely used after stand still.
        /// </summary>
        public static readonly int StartDeltaT = 2000;

        /// <summary>
        /// Fastest DeltaT which is supported
        /// </summary>
        public static int FastestDeltaT = 400;

        /// <summary>
        /// Speed which is safe to turn around without any additional delays.
        /// </summary>
        public static readonly Speed ReverseSafeSpeed = Speed.FromDeltaT(StartDeltaT);

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Speed MaxPlaneSpeed = Speed.FromDeltaT(FastestDeltaT);

        /// <summary>
        /// Maximum speed that is supported for cutting plans.
        /// </summary>
        public static readonly Speed MaxCuttingSpeed = ReverseSafeSpeed;

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Acceleration MaxPlaneAcceleration = new Acceleration(new Speed(MaxAcceleration, TimerFrequency), TimerFrequency);
    }
}
