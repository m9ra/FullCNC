using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    class AccelerationProfile
    {
        public readonly int C0;

        public readonly int StartN;

        public readonly int EndN;

        public readonly int EndDelta;

        public readonly int StartDelta;

        public readonly bool IsDeceleration;

        public readonly long Duration;

        public readonly int StepCount;

        internal AccelerationProfile(int c0, int minimalStartDeltaT, int maximalEndDeltaT, int stepCount, long desiredTickCount, bool isDeceleration)
        {
            C0 = c0;
            IsDeceleration = isDeceleration;
            StepCount = Math.Abs(stepCount);

            int windowStart;
            var window = findClosestWindow(minimalStartDeltaT, maximalEndDeltaT, desiredTickCount, out windowStart);

            if (IsDeceleration)
            {
                //deceleration can be reversed due to careful computation algorithm
                StartN = windowStart + StepCount;
                EndN = windowStart;
                StartDelta = window.Last();
                EndDelta = window.First();
            }
            else
            {
                StartN = windowStart;
                EndN = windowStart + StepCount;
                StartDelta = window.First();
                EndDelta = window.Last();
            }

            Duration = calculateRealDuration();
        }

        private long calculateRealDuration()
        {
            var currentDelta = StartDelta;
            var currentN = StartN;
            var remainderBuffer2 = 0;

            var duration = 0L;
            for (var i = 0; i < StepCount; ++i)
            {
                nextStep_RealDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0)
                    throw new NotSupportedException("Invalid setup");
                duration += currentDelta;
            }
            return duration;
        }

        private int[] findClosestWindow(int minimalStartDelta, int maximalEndDelta, long desiredTickCount, out int windowStart)
        {
            var window = new Queue<int>(StepCount);
            var windowSum = 0L;
            var currentDelta = C0;
            var currentN = 0;
            var remainderBuffer2 = 0;
            while (true)
            {
                //TODO this approach is still imprecise due to remainder buffer (which isn't preserved)
                nextStep_SpeedUpDirection(ref currentDelta, ref currentN, ref remainderBuffer2);
                if (currentDelta < 0)
                    break;

                if (window.Count < StepCount)
                {
                    //window is too small
                    windowSum += currentDelta;
                    window.Enqueue(currentDelta);
                    continue;
                }

                if (window.Peek() < minimalStartDelta)
                    break;

                //now we have to decide if it is better to add next delta instead of last delta
                var popSum = windowSum - window.Peek() + currentDelta;
                var popDiff = Math.Abs(popSum - desiredTickCount);
                var stopDiff = Math.Abs(windowSum - desiredTickCount);
                if (stopDiff < popDiff)
                    //there is no point for continuation
                    //NOTE: we allow equal continuation - after constant segment better value might occur
                    break;

                windowSum -= window.Dequeue();
                windowSum += currentDelta;
                window.Enqueue(currentDelta);

                if (currentDelta > maximalEndDelta)
                    //boundary must be met
                    break;
            }
            windowStart = currentN - window.Count;
            if (window.Count != StepCount)
                throw new NotSupportedException("Invalid setup");
            return window.ToArray();
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
    }
}
