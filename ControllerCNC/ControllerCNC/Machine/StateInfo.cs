using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public struct StateInfo
    {
        /// <summary>
        /// How many ticks was done.
        /// </summary>
        public ulong TickCount { get; private set; }

        /// <summary>
        /// Position along U (in steps).
        /// </summary>
        public int U { get; private set; }

        /// <summary>
        /// Position along V (in steps).
        /// </summary>
        public int V { get; private set; }

        /// <summary>
        /// Position along X (in steps).
        /// </summary>
        public int X { get; private set; }

        /// <summary>
        /// Position along Y (in steps).
        /// </summary>
        public int Y { get; private set; }

        /// <summary>
        /// Determine whether home is calibrated.
        /// </summary>
        public bool IsHomeCalibrated { get; private set; }
        
        internal void SetState(byte[] dataBuffer)
        {
            IsHomeCalibrated = getStateDataBool(dataBuffer, 0);
            X = transformSteps(getStateDataInt32(dataBuffer, 1), Configuration.DirX, Configuration.MaxStepsX);
            Y = transformSteps(getStateDataInt32(dataBuffer, 4 + 1), Configuration.DirY, Configuration.MaxStepsY);
            U = transformSteps(getStateDataInt32(dataBuffer, 4 + 4 + 1), Configuration.DirU, Configuration.MaxStepsU);
            V = transformSteps(getStateDataInt32(dataBuffer, 4 + 4 + 4 + 1), Configuration.DirV, Configuration.MaxStepsV);
        }

        private static int transformSteps(int stepCount, InstructionOrientation dirV, int maxSteps)
        {
            if (dirV == InstructionOrientation.Reversed)
                return maxSteps - stepCount;

            return stepCount;
        }

        internal void Completed(InstructionCNC instruction)
        {
            var homing = instruction as HomingInstruction;
            if (homing != null)
            {
                calibrateHome();
                return;
            }

            var axesInstruction = instruction as Axes;
            if (axesInstruction != null)
            {
                if (axesInstruction.InstructionU != null)
                    U += axesInstruction.InstructionU.HwStepCount;

                if (axesInstruction.InstructionV != null)
                    V += axesInstruction.InstructionV.HwStepCount;

                if (axesInstruction.InstructionX != null)
                    X += axesInstruction.InstructionX.HwStepCount;

                if (axesInstruction.InstructionY != null)
                    Y += axesInstruction.InstructionY.HwStepCount;

                TickCount += axesInstruction.GetInstructionDuration();
                return;
            }

            var stepInstruction = instruction as StepInstrution;
            if (stepInstruction != null)
            {
                X += stepInstruction.HwStepCount;
                TickCount += stepInstruction.GetInstructionDuration();
            }
        }

        internal bool CheckBoundaries()
        {
            if (!IsHomeCalibrated)
            {
                //if calibration is missing it is allowed to move only towards the home switches
                return U <= 0 && V <= 0 && X <= 0 && Y <= 0;
            }

            if (U < 0 || V < 0 || X < 0 || Y < 0)
                return false;

            if (U > Configuration.MaxStepsU || V > Configuration.MaxStepsV || X > Configuration.MaxStepsX || Y > Configuration.MaxStepsY)
                return false;

            return true;
        }

        internal StateInfo Copy()
        {
            //structure will copy - if changed to class, this must be reimplemented!!!
            return this;
        }

        private void calibrateHome()
        {
            X = transformSteps(0, Configuration.DirX, Configuration.MaxStepsX);
            Y = transformSteps(0, Configuration.DirY, Configuration.MaxStepsY);
            U = transformSteps(0, Configuration.DirU, Configuration.MaxStepsU);
            V = transformSteps(0, Configuration.DirV, Configuration.MaxStepsV);

            TickCount = 0;
            IsHomeCalibrated = true;
        }

        private bool getStateDataBool(byte[] buffer, int position)
        {
            return buffer[position] > 0;
        }

        private int getStateDataInt32(byte[] buffer, int position)
        {
            var value = buffer[position + 3] | (buffer[position + 2] << 8) | (buffer[position + 1] << 16) | (buffer[position] << 24);
            return value;
            //return BitConverter.ToInt32(buffer.Skip(position).Take(4).Reverse().ToArray(), 0);
        }
    }
}
