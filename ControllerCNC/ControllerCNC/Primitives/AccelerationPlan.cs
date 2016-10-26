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

        public readonly int EndDeltaT;

        public AccelerationPlan(Int16 stepCount, int startDeltaT, int startN, int endDeltaT)
        {
            StepCount = stepCount;
            StartDeltaT = startDeltaT;
            StartN = startN;
            EndDeltaT = endDeltaT;           
        }

        /// <summary>
        /// Creates inverted acceleration/deceleration profile.
        /// After accelerating according to one, deceleration according the second can be made.
        /// </summary>
        /// <returns>The inverted acceleration.</returns>
        internal AccelerationPlan Invert()
        {
            return new AccelerationPlan(StepCount, EndDeltaT, (Int16)(-StartN - Math.Abs(StepCount)), StartDeltaT);
        }
    }
}
