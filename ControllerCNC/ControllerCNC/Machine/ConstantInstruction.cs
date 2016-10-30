using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public class ConstantInstruction : InstructionCNC
    {
        public readonly Int16 StepCount;

        public readonly int BaseDeltaT;

        public readonly UInt16 PeriodNumerator;

        public readonly UInt16 PeriodDenominator;

        public ConstantInstruction(Int16 stepCount, int baseDeltaT, UInt16 periodNumerator, UInt16 periodDenominator)
        {
            StepCount = stepCount;
            BaseDeltaT = baseDeltaT;
            PeriodNumerator = periodNumerator;
            PeriodDenominator = periodDenominator;
        }

        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'C');
            sendBuffer.AddRange(ToBytes(StepCount));
            sendBuffer.AddRange(ToBytes(BaseDeltaT));
            sendBuffer.AddRange(ToBytes(PeriodNumerator));
            sendBuffer.AddRange(ToBytes(PeriodDenominator));
            return sendBuffer.ToArray();
        }

        /// </inheritdoc>
        public override string ToString()
        {
            return string.Format("C({0},{1},{2},{3}", StepCount, BaseDeltaT, PeriodNumerator, PeriodDenominator);
        }

    }
}
