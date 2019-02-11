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
    public static class Configuration
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
        /// How close two consequitive activations can go one by another
        /// </summary>
        public static readonly int MinActivationDelay = 10 * 2;

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
        /// Maximal safe acceleration when accelerating many steps apart steps/s^2.
        /// </summary
        public static readonly int MaxPartialAcceleration = 2000 * StepsPerRevolution;

        /// <summary>
        /// U axis is 460mm long
        /// </summary>
        internal static readonly int HwMaxStepsU = 460 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// V axis is 256mm long
        /// </summary>
        internal static readonly int HwMaxStepsV = 256 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// X axis is 460mm long
        /// </summary>
        internal static readonly int HwMaxStepsX = 460 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// Y axis is 256mm long
        /// </summary>
        internal static readonly int HwMaxStepsY = 256 * StepsPerRevolution * 100 / 125;

        /// <summary>
        /// Baud rate for serial communication.
        /// </summary>
        internal static readonly int CommunicationBaudRate = 128000;

        /// <summary>
        /// Length of the instruction sent to machine.
        /// </summary>
        internal static readonly int InstructionLength = 59;

        /// <summary>
        /// How many instructions fit into the buffer.
        /// </summary>
        internal static int InstructionBufferLimit = 6;

        /// <summary>
        /// How many steps can be stored in step buffer.
        /// </summary>
        internal static int StepBufferSize = 256;

        /// <summary>
        /// Size of machine state buffer.
        /// </summary>
        internal static readonly int MachineStateBufferSize = 1 + 4 * 4;

        /// <summary>
        /// Max allowed steps along X axis.
        /// </summary>
        public static int MaxStepsX { get; private set; }

        /// <summary>
        /// Max allowed steps along Y axis.
        /// </summary>
        public static int MaxStepsY { get; private set; }

        /// <summary>
        /// Max allowed steps along U axis.
        /// </summary>
        public static int MaxStepsU { get; private set; }

        /// <summary>
        /// Max allowed steps along V axis.
        /// </summary>
        public static int MaxStepsV { get; private set; }

        /// <summary>
        /// Direction mapping to the machine. TODO refactor out of driver.
        /// </summary>
        internal static InstructionOrientation DirU { get; private set; }

        /// <summary>
        /// Direction mapping to the machine. TODO refactor out of driver.
        /// </summary>
        internal static InstructionOrientation DirV { get; private set; }

        /// <summary>
        /// Direction mapping to the machine. TODO refactor out of driver.
        /// </summary>
        internal static InstructionOrientation DirX { get; private set; }

        /// <summary>
        /// Direction mapping to the machine. TODO refactor out of driver.
        /// </summary>
        internal static InstructionOrientation DirY { get; private set; }

        /// <summary>
        /// Determine whether router mode with Y to V axis switched is enabled.
        /// </summary>
        public static bool IsRouterModeEnabled { get; private set; }

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
        /// Metric version of <see cref="ReverseSafeSpeed"/>.
        /// </summary>
        public static readonly double ReverseSafeSpeedMetric = ReverseSafeSpeed.ToMetric();

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Speed MaxPlaneSpeed = Speed.FromDeltaT(FastestDeltaT);

        /// <summary>
        /// Metric version of <see cref="MaxPlaneSpeed"/>.
        /// </summary>
        public static readonly double MaxPlaneSpeedMetric = MaxPlaneSpeed.ToMetric();

        /// <summary>
        /// Maximum speed that is supported for cutting plans.
        /// </summary>
        public static readonly Speed MaxCuttingSpeed = ReverseSafeSpeed;

        /// <summary>
        /// Maximal speed for head moving in a plane (X,Y or U,V).
        /// </summary>
        public static readonly Acceleration MaxPlaneAcceleration = new Acceleration(new Speed(MaxAcceleration, TimerFrequency), TimerFrequency);

        static Configuration()
        {
            MaxStepsU = HwMaxStepsU;
            MaxStepsV = HwMaxStepsV;
            MaxStepsX = HwMaxStepsX;
            MaxStepsY = HwMaxStepsY;

            DirU = DirV = DirY = DirX = InstructionOrientation.Normal;
        }

        /// <summary>
        /// Enable milling router limitations.
        /// Milling router limitations are also considered.
        /// </summary>
        public static void EnableRouterMode()
        {
            MaxStepsY = 200 * StepsPerRevolution * 100 / 125;
            //MaxStepsV = 285 * StepsPerRevolution * 100 / 125;
            MaxStepsV = 385 * StepsPerRevolution * 100 / 125;

            MaxStepsU = MaxStepsV; //artificial limitation
            MaxStepsX = MaxStepsV;//artificial limitation
            IsRouterModeEnabled = true;
            DirU = DirX = InstructionOrientation.Reversed;
        }
    }
}
