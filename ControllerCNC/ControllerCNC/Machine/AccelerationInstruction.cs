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
            : base(stepCount, InstructionOrientation.Normal)
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

        private AccelerationInstruction(AccelerationInstruction originalInstruction, InstructionOrientation newOrientation)
            : base(originalInstruction.StepCount, newOrientation)
        {
            InitialDeltaT = originalInstruction.InitialDeltaT;
            StartN = originalInstruction.StartN;
            BaseDelta = originalInstruction.BaseDelta;
            BaseRemainder = originalInstruction.BaseRemainder;
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
            sendBuffer.AddRange(ToBytes((Int16)(StepCount * (Int16)Orientation)));
            sendBuffer.AddRange(ToBytes(InitialDeltaT));
            sendBuffer.AddRange(ToBytes(StartN));
            sendBuffer.AddRange(ToBytes(BaseDelta));
            sendBuffer.AddRange(ToBytes(BaseRemainder));

            return sendBuffer.ToArray();
        }

        /// </inheritdoc>
        internal override int[] GetStepTimings()
        {
            var absStepCount = Math.Abs(StepCount);
            var isDeceleration = StartN < 0;
            var baseDeltaT = BaseDelta;
            var baseRemainder = Math.Abs(BaseRemainder);
            var baseRemainderBuffer = baseRemainder / 2;
            var currentDeltaT = InitialDeltaT;
            var current4N = 4 * Math.Abs(StartN);
            var currentDeltaTBuffer2 = 0;

            var timing = new int[absStepCount];
            for (var i = 0; i < absStepCount; ++i)
            {
                var nextActivationTime = currentDeltaT + baseDeltaT;
                if (baseRemainder > 0)
                {
                    baseRemainderBuffer += baseRemainder;
                    if (baseRemainderBuffer > absStepCount)
                    {
                        baseRemainderBuffer -= absStepCount;
                        nextActivationTime += 1;
                    }
                }

                if (current4N == 0)
                    //compensate for error at c0
                    currentDeltaT = currentDeltaT * 676 / 1000;

                var nextDeltaT = currentDeltaT;
                var nextDeltaTChange = 0;
                currentDeltaTBuffer2 += nextDeltaT * 2;

                if (isDeceleration)
                    current4N -= 4;
                else
                    current4N += 4;

                nextDeltaTChange = currentDeltaTBuffer2 / (current4N + 1);
                currentDeltaTBuffer2 = currentDeltaTBuffer2 % (current4N + 1);
                nextDeltaT = isDeceleration ? nextDeltaT + nextDeltaTChange : nextDeltaT - nextDeltaTChange;
                currentDeltaT = nextDeltaT;

                timing[i] = nextActivationTime;
                if (StepCount < 0)
                    timing[i] *= -1;
            }

            return timing;
        }

        /// </inheritdoc>
        public override string ToString()
        {
            return string.Format("A({0},{1},{2},{3},{4},{5})", StepCount, InitialDeltaT, StartN, BaseRemainder, BaseDelta, Orientation);
        }

        /// </inheritdoc>
        internal override ulong GetInstructionDuration()
        {
            return (ulong)GetStepTimings().Select(s => Math.Abs(s)).Sum();
        }

        /// </inheritdoc>
        internal override StepInstrution WithOrientation(InstructionOrientation orientation)
        {
            if (Orientation == orientation)
                return this;

            return new AccelerationInstruction(this, orientation);
        }
    }
}
