using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public class ConstantInstruction : StepInstrution
    {
        public readonly int BaseDeltaT;

        public readonly UInt16 PeriodNumerator;

        public readonly UInt16 PeriodDenominator;

        public ConstantInstruction(Int16 stepCount, int baseDeltaT, UInt16 periodNumerator)
            : base(stepCount, InstructionOrientation.Normal)
        {
            BaseDeltaT = baseDeltaT;
            PeriodNumerator = periodNumerator;
            if (PeriodNumerator > Math.Abs(StepCount))
                throw new NotSupportedException("Invalid numerator value");

            PeriodDenominator = (UInt16)Math.Abs(StepCount);
        }

        private ConstantInstruction(ConstantInstruction originalInstruction, InstructionOrientation newOrientation)
            : base(originalInstruction.StepCount, newOrientation)
        {
            BaseDeltaT = originalInstruction.BaseDeltaT;
            PeriodNumerator = originalInstruction.PeriodNumerator;
            PeriodDenominator = originalInstruction.PeriodDenominator;
        }

        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'C');
            sendBuffer.AddRange(ToBytes((Int16)(StepCount * (Int16)Orientation)));
            sendBuffer.AddRange(ToBytes(BaseDeltaT));
            sendBuffer.AddRange(ToBytes(PeriodNumerator));
            sendBuffer.AddRange(ToBytes(PeriodDenominator));
            return sendBuffer.ToArray();
        }

        /// </inheritdoc>
        internal override int[] GetStepTimings()
        {
            var result = new int[Math.Abs(StepCount)];
            var periodAccumulator = PeriodNumerator / 2;
            for (var i = 0; i < Math.Abs(StepCount); ++i)
            {
                var activationTime = BaseDeltaT;
                if (PeriodNumerator > 0)
                {
                    periodAccumulator += PeriodNumerator;
                    if (PeriodDenominator >= periodAccumulator)
                    {
                        periodAccumulator -= PeriodDenominator;
                        activationTime += 1;
                    }
                }

                result[i] = activationTime;
                if (StepCount < 0)
                    result[i] *= -1 * (int)Orientation;
            }
            return result;
        }

        /// </inheritdoc>
        public override string ToString()
        {
            return string.Format("C({0},{1},{2},{3},{4})", StepCount, BaseDeltaT, PeriodNumerator, PeriodDenominator, Orientation);
        }

        /// </inheritdoc>
        internal override ulong GetInstructionDuration()
        {
            var result = (ulong)Math.Abs(StepCount) * (ulong)BaseDeltaT;
            if (PeriodDenominator > 0)
                result += (ulong)(PeriodNumerator / PeriodDenominator);

            return result;
        }

        /// </inheritdoc>
        internal override StepInstrution WithOrientation(InstructionOrientation orientation)
        {
            if (Orientation == orientation)
                return this;

            return new ConstantInstruction(this, orientation);
        }
    }
}
