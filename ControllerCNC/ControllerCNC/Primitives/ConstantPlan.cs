using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    public class ConstantPlan : Plan
    {
        public readonly Int16 StepCount;

        public readonly int BaseDelta;

        public readonly UInt16 PeriodNumerator;

        public readonly UInt16 PeriodDenominator;

        public ConstantPlan(Int16 stepCount, int baseDelta, UInt16 periodNumerator, UInt16 periodDenominator)
        {
            StepCount = stepCount;
            BaseDelta = baseDelta;
            PeriodNumerator = periodNumerator;
            PeriodDenominator = periodDenominator;
        }
    }
}
