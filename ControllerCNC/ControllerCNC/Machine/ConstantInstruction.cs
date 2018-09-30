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

        public readonly int Offset;

        public readonly UInt16 PeriodNumerator;

        public bool HasOffset => Offset != -2147483648;

        public ConstantInstruction(Int16 stepCount, int baseDeltaT, UInt16 periodNumerator, int offset = -2147483648)
            : base(stepCount, InstructionOrientation.Normal)
        {
            BaseDeltaT = baseDeltaT;
            PeriodNumerator = periodNumerator;
            Offset = offset;
            if (PeriodNumerator > Math.Abs(StepCount))
                throw new NotSupportedException("Invalid numerator value");

            if (Math.Abs(BaseDeltaT) + Offset < 0 && HasOffset)
                throw new NotSupportedException("Invalid offset value");
        }

        private ConstantInstruction(ConstantInstruction originalInstruction, InstructionOrientation newOrientation)
            : base(originalInstruction.StepCount, newOrientation)
        {
            BaseDeltaT = originalInstruction.BaseDeltaT;
            PeriodNumerator = originalInstruction.PeriodNumerator;
            Offset = originalInstruction.Offset;
        }

        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'C');
            sendBuffer.AddRange(ToBytes((Int16)(StepCount * (Int16)Orientation)));
            sendBuffer.AddRange(ToBytes(BaseDeltaT));
            sendBuffer.AddRange(ToBytes(PeriodNumerator));
            sendBuffer.AddRange(ToBytes(Offset));
            return sendBuffer.ToArray();
        }

        /// </inheritdoc>
        internal override int[] GetStepTimings()
        {
            var result = new int[Math.Abs(StepCount)];
            var periodAccumulator = 0;
            var periodDenominator = Math.Abs(StepCount);
            if (PeriodNumerator > 0)
                periodAccumulator = periodDenominator / PeriodNumerator;

            var offset = HasOffset ? Offset : 0;

            for (var i = 0; i < Math.Abs(StepCount); ++i)
            {
                var activationTime = BaseDeltaT;
                if (PeriodNumerator > 0)
                {
                    periodAccumulator += PeriodNumerator;
                    if (periodDenominator < periodAccumulator)
                    {
                        periodAccumulator -= periodDenominator;
                        activationTime += 1;
                    }
                }

                activationTime += offset;
                offset = 0;

                result[i] = activationTime;
                if (StepCount < 0)
                    result[i] *= -1 * (int)Orientation;
            }
            return result;
        }

        /// </inheritdoc>
        public override string ToString()
        {
            return string.Format("C({0},{1},{2},{3},{4})", StepCount, BaseDeltaT, PeriodNumerator, Offset, Orientation);
        }

        /// </inheritdoc>
        internal override ulong GetInstructionDuration()
        {
            var result = ((ulong)Math.Abs(StepCount)) * ((ulong)Math.Abs(BaseDeltaT));
            var periodDenominator = (ushort)Math.Abs(StepCount);
            if (periodDenominator > 0)
                result += PeriodNumerator * (ulong)Math.Abs(StepCount) / periodDenominator;

            if (HasOffset)
                result = (ulong)((long)result + Offset);
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
