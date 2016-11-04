using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    class AccelerationInstruction : StepInstrution
    {
        public readonly int InitialDeltaT;

        public readonly int StartN;

        public readonly Int16 BaseDelta;

        public readonly Int16 BaseRemainder;

        public AccelerationInstruction(Int16 stepCount, int initialDeltaT, Int16 baseDelta, Int16 baseRemainder, int startN)
            :base(stepCount)
        {
            InitialDeltaT = initialDeltaT;
            StartN = startN;
            BaseDelta = baseDelta;
            BaseRemainder = baseRemainder;

            if (initialDeltaT < 0)
                throw new NotSupportedException("Negative delta");

            if (StartN < 0 && Math.Abs(stepCount) > -StartN)
                throw new NotSupportedException("Invalid StartN value");
        }


        internal AccelerationInstruction WithReversedDirection()
        {
            return new AccelerationInstruction((Int16)(-StepCount), InitialDeltaT, BaseDelta, BaseRemainder, StartN);
        }

        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'A');
            sendBuffer.AddRange(ToBytes(StepCount));
            sendBuffer.AddRange(ToBytes(InitialDeltaT));
            sendBuffer.AddRange(ToBytes(StartN));
            sendBuffer.AddRange(ToBytes(BaseDelta));
            sendBuffer.AddRange(ToBytes(BaseRemainder));

            return sendBuffer.ToArray();
        }

        /// </inheritdoc>
        public override string ToString()
        {
            return string.Format("A({0},{1},{2})", StepCount, InitialDeltaT, StartN);
        }
    }
}
