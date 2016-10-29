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

            c0 = Math.Abs(Math.Round(c0 * 0.676));
            findInitialDeltaT((int)c0, targetDelta, out InitialDeltaT, out InitialN);
            StartDeltaT = InitialDeltaT;

            checked
            {
                var sequence = calculateRealSequence();
                var accelerationTickCount = sequence.Sum();
                var tickCountDifference = tickCount - accelerationTickCount;
                var desiredBaseDelta = tickCountDifference / StepCount;
                if (desiredBaseDelta > Int16.MaxValue)
                    //TODO - this has to be repaired !!!
                    desiredBaseDelta = Int16.MaxValue;
                if (desiredBaseDelta < Int16.MinValue)
                    //TODO - this has to be repaired !!!
                    desiredBaseDelta = Int16.MinValue;

                BaseDelta = (Int16)(desiredBaseDelta);
                if (tickCountDifference < 0)
                    BaseDelta -= 1;
                TotalTickCount = BaseDelta * StepCount + accelerationTickCount;
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

        internal AccelerationProfile(int startDelta, int endDelta, int stepCount, long desiredTickCount)
        {
            if (startDelta < 0 || endDelta < 0)
                throw new NotSupportedException("Delta tick time has to be positive");

            if (stepCount < 0)
                throw new NotSupportedException("Step count has to be positive");

            InitialDeltaT = startDelta;
            StartDeltaT = startDelta;
            EndDelta = endDelta;
            IsDeceleration = startDelta < endDelta;
            StepCount = Math.Abs(stepCount);
            var deltaChange = endDelta - startDelta;

            findWindow(deltaChange, out InitialN, out InitialDeltaT, desiredTickCount);

            checked
            {
                var window = calculateRealSequence();
                var accelerationTickCount = window.Sum();
                var tickCountDifference = desiredTickCount - accelerationTickCount;
                BaseDelta = (Int16)(tickCountDifference / stepCount);
                TotalTickCount = BaseDelta * stepCount + accelerationTickCount;
            }
        }

        private long[] calculateRealSequence()
        {
            var currentDelta = InitialDeltaT;
            var currentN = InitialN;
            var remainderBuffer2 = 0;

            var window = new List<long>();
            for (var i = 0; i < StepCount; ++i)
            {
                nextStep_RealDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0)
                    throw new NotSupportedException("Invalid setup");
                window.Add(currentDelta);
            }
            return window.ToArray();
        }

        private void findWindow(int deltaChange, out int initialN, out int currentStartDelta, long desiredDuration)
        {
            currentStartDelta = StartDeltaT + Math.Abs(deltaChange);
            initialN = IsDeceleration ? StepCount : 0;

            for (var attemptIndex = 0; attemptIndex < 100; ++attemptIndex)
            {
                var duration = optimizeDeltaChange(deltaChange, desiredDuration, ref currentStartDelta, ref initialN);

                var isSufficientSolution = Math.Abs(duration - desiredDuration) < StepCount * (Int16.MaxValue - 1);
                if (isSufficientSolution)
                {
                    return;
                }

                optimizeDuration(desiredDuration, ref currentStartDelta, ref initialN);
                //throw new NotImplementedException("We have solution");
            }


            throw new NotImplementedException("find better searching algorithm");
        }

        private void optimizeDuration(long desiredDuration, ref int currentStartDelta, ref int initialN)
        {
            int currentDeltaChange;
            var duration = getDuration(currentStartDelta, initialN, out currentDeltaChange);
            var wasTooLong = duration > desiredDuration;

            var distanceOptimizationFactor = 0.9;
            var exactCurrentStartDelta = 1.0 * currentStartDelta;
            while (desiredDuration != duration && distanceOptimizationFactor > 0.001)
            {
                if (duration > desiredDuration)
                {
                    //make the curve more shallow
                    if (!wasTooLong)
                        distanceOptimizationFactor *= 0.7;
                    wasTooLong = true;
                    exactCurrentStartDelta = exactCurrentStartDelta * (1.0 - distanceOptimizationFactor);
                }
                else if (duration < desiredDuration)
                {
                    if (wasTooLong)
                        distanceOptimizationFactor *= 0.7;
                    wasTooLong = false;
                    exactCurrentStartDelta = exactCurrentStartDelta * (1.0 + distanceOptimizationFactor);
                }
                currentStartDelta = (int)exactCurrentStartDelta;
                duration = getDuration(currentStartDelta, initialN, out currentDeltaChange);
            }
        }

        private long optimizeDeltaChange(int desiredDeltaChange, long desiredDuration, ref int currentStartDelta, ref int initialN)
        {
            var currentDuration = 0L;
            var currentDeltaChange = 0;

            var startDeltaChangeFactor = currentStartDelta / 10;
            while (true)
            {
                currentDuration = getDuration(currentStartDelta, initialN, out currentDeltaChange);
                if (currentDeltaChange == desiredDeltaChange)
                    break;

                var increaseStartDelta = improvementRank(desiredDuration, desiredDeltaChange, currentDuration, currentDeltaChange, initialN, currentStartDelta + startDeltaChangeFactor);

                var decreaseStartDelta = improvementRank(desiredDuration, desiredDeltaChange, currentDuration, currentDeltaChange, initialN, currentStartDelta - startDeltaChangeFactor);


                var increaseN = improvementRank(desiredDuration, desiredDeltaChange, currentDuration, currentDeltaChange, initialN + 1, currentStartDelta);
                var decreaseN = improvementRank(desiredDuration, desiredDeltaChange, currentDuration, currentDeltaChange, initialN - 1, currentStartDelta);

                var hasImprovement = false;
                foreach (var changeRank in new[] { 10, 5 })
                {
                    if (hasImprovement)
                        break;

                    if (increaseN >= changeRank)
                    {
                        ++initialN;
                        hasImprovement = true;
                        continue;
                    }
                    if (decreaseN >= changeRank)
                    {
                        --initialN;
                        hasImprovement = true;
                        continue;
                    }
                    if (increaseStartDelta >= changeRank)
                    {
                        currentStartDelta += startDeltaChangeFactor;
                        hasImprovement = true;
                        continue;
                    }
                    if (decreaseStartDelta >= changeRank)
                    {
                        currentStartDelta -= startDeltaChangeFactor;
                        hasImprovement = true;
                        continue;
                    }
                }
                if (!hasImprovement)
                    startDeltaChangeFactor = (int)Math.Max(startDeltaChangeFactor * 0.95, 1);
            }

            return currentDuration;
        }

        private int improvementRank(long desiredDuration, int desiredDeltaChange, long currentDuration, int currentDeltaChange, int initialN, int startDeltaT)
        {
            var newDeltaChange = 0;
            var currentDurationDiff = Math.Abs(desiredDuration - currentDuration);
            var currentDeltaDiff = Math.Abs(desiredDeltaChange - currentDeltaChange);

            var newDuration = getDuration(startDeltaT, initialN, out newDeltaChange);
            var isDurationImprovement = Math.Abs(desiredDuration - newDuration) < currentDurationDiff;
            var isDeltaChangeImprovement = Math.Abs(desiredDeltaChange - newDeltaChange) < currentDeltaDiff;

            if (isDurationImprovement && isDeltaChangeImprovement)
                return 10;

            if (isDeltaChangeImprovement)
                return 5;

            if (isDurationImprovement)
                return 1;

            return 0;
        }

        private long getDuration(int currentStartDelta, int initialN, out int currentDeltaChange)
        {
            var currentDelta = currentStartDelta;
            var remainderBuffer2 = 0;
            var currentN = initialN;
            var realStepCount = 0;
            var totalTime = 0L;
            for (var i = 0; i < StepCount; ++i)
            {
                nextStep_RealDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0 || currentN < 0)
                {
                    //the valid distance is too short
                    totalTime = i - StepCount;
                    break;
                }

                ++realStepCount;
                totalTime += currentDelta;
            }

            currentDeltaChange = currentDelta - currentStartDelta;
            return totalTime;
        }

        /// <summary>
        /// This steps acceleration/deceleration both from lowest to highest speeds (deceleration is reversed therefore!)
        /// </summary>
        private void nextStep_SpeedUpDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
            //if (IsDeceleration)
            //    throw new NotImplementedException("Find formula which will be consistent with deceleration");

            ++currentN;
            remainderBuffer2 += 2 * currentDelta;
            var change = remainderBuffer2 / (4 * currentN + 1);
            remainderBuffer2 = remainderBuffer2 % (4 * currentN + 1);

            currentDelta = currentDelta - change;
        }

        /// <summary>
        /// This steps acceleration/deceleration in precisely same way as the CNC implementation.
        /// </summary>
        private void nextStep_RealDirection(ref int currentDelta, ref int currentN, ref int remainderBuffer2)
        {
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

            return string.Format(prefix + "({0}, {1}, {2}, {3}:{4})", StepCount, TotalTickCount, InitialN, StartDeltaT, EndDelta);
        }
    }
}
