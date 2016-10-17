using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    class Acceleration
    {
        public readonly Int16 StepCount;

        public readonly UInt16 StartDeltaT;

        public readonly Int16 StartN;

        public readonly UInt16 EndDeltaT;

        public Acceleration(Int16 stepCount, UInt16 startDeltaT, Int16 startN, UInt16 endDeltaT)
        {
            StepCount = stepCount;
            StartDeltaT = startDeltaT;
            StartN = startN;
            EndDeltaT = endDeltaT;

            if (StartN == 0)
                throw new NotSupportedException("Invalid acceleration profile");
        }

        /// <summary>
        /// Creates inverted acceleration/deceleration profile.
        /// After accelerating according to one, deceleration according the second can be made.
        /// </summary>
        /// <returns>The inverted acceleration.</returns>
        internal Acceleration Invert()
        {
            return new Acceleration(StepCount, EndDeltaT, (Int16)(-StartN - Math.Abs(StepCount)), StartDeltaT);
        }
    }
}
