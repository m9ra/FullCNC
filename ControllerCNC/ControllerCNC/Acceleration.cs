using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC
{
    class Acceleration
    {
        public readonly Int16 StepCount;
        public readonly Int16 AccelerationNumerator;
        public readonly Int16 AccelerationDenominator;
        public readonly UInt16 InitialDeltaT;

        public Acceleration(Int16 stepCount, Int16 accelerationNumerator, Int16 accelerationDenominator, UInt16 initialDeltaT)
        {
            StepCount = stepCount;
            AccelerationNumerator = accelerationNumerator;
            AccelerationDenominator = accelerationDenominator;
            InitialDeltaT = initialDeltaT;
        }
    }
}
