using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    class AccelerationPlan : Plan
    {
        public readonly Int16 StepCount;

        public readonly int StartDeltaT;

        public readonly int StartN;

        public AccelerationPlan(Int16 stepCount, int startDeltaT, int startN)
        {
            StepCount = stepCount;
            StartDeltaT = startDeltaT;
            StartN = startN;

            if (startDeltaT < 0)
                throw new NotSupportedException("Negative delta");

            if (StartN < 0 && Math.Abs(stepCount) > -StartN)
                throw new NotSupportedException("Invalid StartN value");
        }

        /// <summary>
        /// Creates inverted acceleration/deceleration profile.
        /// After accelerating according to one, deceleration according the second can be made.
        /// </summary>
        /// <returns>The inverted acceleration.</returns>
        internal AccelerationPlan Invert()
        {
            throw new NotImplementedException("No more acceleration is reversible");
        }

        public override string ToString()
        {
            return string.Format("A({0},{1},{2})", StepCount, StartDeltaT, StartN);
        }
    }
}
