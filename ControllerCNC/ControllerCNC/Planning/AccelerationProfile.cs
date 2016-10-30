using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    class AccelerationProfile
    {
        /// <summary>
        /// Initial N which is a parameter of acceleration
        /// </summary>
        public readonly int InitialN;

        /// <summary>
        /// Initial delta which is a parameter of acceleration.
        /// </summary>
        public readonly int InitialDeltaT;

        /// <summary>
        /// Delta which is used to compensate acceleration duration
        /// </summary>
        public readonly Int16 BaseDelta;

        /// <summary>
        /// Delta which is used to compensate acceleration duration
        /// </summary>
        public readonly Int16 BaseRemainder;

        /// <summary>
        /// How many steps will be with the acceleration.
        /// </summary>
        public readonly int StepCount;

        /// <summary>
        /// Determines whether acceleration speeds up or slows down the movement.
        /// </summary>
        public readonly bool IsDeceleration;

        /// <summary>
        /// Real delta which this acceleration is continuing from
        /// </summary>
        public readonly int StartDeltaT;

        /// <summary>
        /// End delta which could be continued after acceleration
        /// </summary>
        public readonly int EndDelta;

        /// <summary>
        /// How many ticks the acceleration lasts.
        /// </summary>
        public readonly long TotalTickCount;

        internal AccelerationProfile(double c0, int targetDelta, int stepCount, long tickCount)
        {
            if (targetDelta < 0)
                throw new NotSupportedException("Target delta has to be positive");

            StepCount = Math.Abs(stepCount);
            IsDeceleration = c0 < 0;
            EndDelta = targetDelta;

            c0 = Math.Abs(Math.Round(c0));
            findInitialDeltaT((int)c0, targetDelta, out InitialDeltaT, out InitialN);
            StartDeltaT = InitialDeltaT;

            checked
            {
                var sequence = calculateRealSequence();

                var accelerationTickCount = sequence.Sum();
                var tickCountDifference = tickCount - accelerationTickCount;
                var desiredBaseDelta = tickCountDifference / StepCount;
                if (desiredBaseDelta > Int16.MaxValue)
                    throw new NotSupportedException("Acceleration error is out of bounds");
                if (desiredBaseDelta < Int16.MinValue)
                    throw new NotSupportedException("Acceleration error is out of bounds");

                BaseDelta = (Int16)(desiredBaseDelta);
                if (tickCountDifference < 0)
                    BaseDelta -= 1;

                TotalTickCount = BaseDelta * StepCount + accelerationTickCount;
                BaseRemainder = (Int16)(tickCount - TotalTickCount);
                TotalTickCount += BaseRemainder;
            }
        }

        private void findInitialDeltaT(int c0, int targetDelta, out int globalInitialDeltaT, out int globalInitialN)
        {
            var minimalInitialN = IsDeceleration ? StepCount : 0;
            globalInitialDeltaT = c0;
            globalInitialN = 0;
            var globalRemainderBuffer2 = 0;
            while (true)
            {
                if (globalInitialN >= minimalInitialN)
                {
                    //try if the initial conditions match the requirements
                    var endDelta = getEndDelta(globalInitialDeltaT, globalInitialN);
                    if (targetDelta >= endDelta)
                        return;
                }

                nextStep_SpeedUpDirection(ref globalInitialDeltaT, ref globalInitialN, ref globalRemainderBuffer2);
            }
        }

        private int getEndDelta(int initialDeltaT, int initialN)
        {
            var remainderBuffer2 = 0;
            for (var i = 0; i < StepCount; ++i)
            {
                nextStep_RealDirection(ref initialDeltaT, ref initialN, ref remainderBuffer2);
                if (initialN < 0 || initialDeltaT < 0)
                    throw new NotSupportedException("Invalid values");
            }

            if (IsDeceleration && initialN == 0)
                //deceleration to standstill
                return int.MaxValue;

            nextStep_RealDirection(ref initialDeltaT, ref initialN, ref remainderBuffer2);
            return initialDeltaT;
        }



        private long[] calculateRealSequence()
        {
            var currentDelta = InitialDeltaT;
            var currentN = InitialN;
            var remainderBuffer2 = 0;

            var window = new List<long>();
            for (var i = 0; i < StepCount; ++i)
            {
                window.Add(currentDelta);
                nextStep_RealDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0)
                    throw new NotSupportedException("Invalid setup");

            }
            return window.ToArray();
        }

        /// <summary>
        /// This steps acceleration/deceleration both from lowest to highest speeds (deceleration is reversed therefore!)
        /// </summary>
        private void nextStep_SpeedUpDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
            if (currentN == 0) checked
                {
                    //compensate for initial error (TODO include deceleration properly)
                    currentDelta = currentDelta * 676 / 1000;
                }

            ++currentN;
            remainderBuffer2 += 2 * currentDelta;
            var change = remainderBuffer2 / (4 * currentN + 1);
            remainderBuffer2 = remainderBuffer2 % (4 * currentN + 1);

            currentDelta = currentDelta - change;

            //System.Diagnostics.Debug.WriteLine(currentDelta + " " + currentN + " " + remainderBuffer2);
        }

        /// <summary>
        /// This steps acceleration/deceleration in precisely same way as the CNC implementation.
        /// </summary>
        private void nextStep_RealDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
            if (currentN == 0) checked
                {
                    //compensate for initial error
                    currentDelta = currentDelta * 676 / 1000;
                }

            currentN += IsDeceleration ? -1 : 1;
            remainderBuffer2 += 2 * currentDelta;
            var change = remainderBuffer2 / (4 * currentN + 1);
            remainderBuffer2 = remainderBuffer2 % (4 * currentN + 1);

            if (IsDeceleration)
                change *= -1;

            currentDelta = currentDelta - change;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var prefix = IsDeceleration ? "PD" : "PA";

            return string.Format(prefix + "({0}, {1}, {2}, {3}:{4})", StepCount, TotalTickCount, InitialN, StartDeltaT + BaseDelta, EndDelta + BaseDelta);
        }
    }
}
