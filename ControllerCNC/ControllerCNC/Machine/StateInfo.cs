using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    struct StateInfo
    {
        /// <summary>
        /// Position along U (in steps).
        /// </summary>
        internal int U { get; private set; }

        /// <summary>
        /// Position along V (in steps).
        /// </summary>
        internal int V { get; private set; }

        /// <summary>
        /// Position along X (in steps).
        /// </summary>
        internal int X { get; private set; }

        /// <summary>
        /// Position along Y (in steps).
        /// </summary>
        internal int Y { get; private set; }

        /// <summary>
        /// Determine whether home is calibrated.
        /// </summary>
        internal bool IsHomeCalibrated { get; private set; }

        internal void SetState(byte[] dataBuffer)
        {
            IsHomeCalibrated = getStateDataBool(dataBuffer, 0);
            X = getStateDataInt32(dataBuffer, 1);
            Y = getStateDataInt32(dataBuffer, 4 + 1);
            U = getStateDataInt32(dataBuffer, 4 + 4 + 1);
            V = getStateDataInt32(dataBuffer, 4 + 4 + 4 + 1);
        }

        internal void CalibrateHome()
        {
            IsHomeCalibrated = true;
            U = 0;
            V = 0;
            X = 0;
            Y = 0;
        }

        internal void Completed(InstructionCNC instruction)
        {
            var axesInstruction = instruction as Axes;
            if (axesInstruction != null)
            {
                if (axesInstruction.InstructionU != null)
                    U += axesInstruction.InstructionU.StepCount;

                if (axesInstruction.InstructionV != null)
                    V += axesInstruction.InstructionV.StepCount;

                if (axesInstruction.InstructionX != null)
                    X += axesInstruction.InstructionX.StepCount;

                if (axesInstruction.InstructionY != null)
                    Y += axesInstruction.InstructionY.StepCount;
                return;
            }

            var stepInstruction = instruction as StepInstrution;
            if (stepInstruction != null)
            {
                X += stepInstruction.StepCount;
            }
        }

        internal bool CheckBoundaries()
        {
            if (U < 0 || V < 0 || X < 0 || Y < 0)
                return false;

            if (U > Constants.MaxStepsU || V > Constants.MaxStepsV || X > Constants.MaxStepsX || Y > Constants.MaxStepsY)
                return false;

            return true;
        }

        internal StateInfo Copy()
        {
            //structure will copy - if changed to class, this must be reimplemented!!!
            return this;
        }

        private bool getStateDataBool(byte[] buffer, int position)
        {
            return buffer[position] > 0;
        }

        private int getStateDataInt32(byte[] buffer, int position)
        {
            return BitConverter.ToInt32(buffer.Skip(position).Take(4).Reverse().ToArray(), 0);
        }
    }
}
