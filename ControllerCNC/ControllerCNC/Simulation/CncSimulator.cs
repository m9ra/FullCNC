using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrajectorySimulator
{
    class CncSimulator
    {


        public void CalculateAcceleration(int stepCount, int initialDelta, int initialN, ChannelTrace trace)
        {
            if (initialN < 0)
                throw new NotImplementedException("deceleration");

            var currentDelta = (int)(initialDelta * 0.676);

            var currentN = initialN;
            var accumulator = 0;
            var rest = 0;
            for (var i = 0; i < stepCount; ++i)
            {
                /*
                accumulator += currentDelta;
                ++currentN;

                while (2 * accumulator > 4 * currentN - 1)
                {
                    accumulator -= (4 * currentN - 1) / 2;
                    --currentDelta;
                }*/
                ++currentN;
                var newDelta = currentDelta - ((2 * currentDelta + rest) / (4 * currentN + 1));
                rest = (2 * currentDelta + rest) % (4 * currentN + 1);


                trace.AddTime((int)currentDelta);
            }
        }

        internal void CalculateAccelerationExact(double initialSpeed, double desiredSpeed, double acceleration, ChannelTrace trace)
        {
            var deltaSpeed = desiredSpeed - initialSpeed;
            var distance = deltaSpeed / acceleration;

            var lastStep = 0;
            var lastStepTime = 0.0;
            var currentDistance = 0.0;
            var currentTime = 0.0;
            var currentSpeed = initialSpeed;
            while (currentSpeed <= desiredSpeed)
            {
                currentTime += 0.00000001;
                currentSpeed = initialSpeed + currentTime * acceleration;
                currentDistance = initialSpeed * currentTime + 0.5 * acceleration * currentTime * currentTime;
                var currentStep = (int)currentDistance;

                if (currentStep != lastStep)
                {
                    var deltaTime = currentTime - lastStepTime;
                    var deltaTimeScaled = (int)Math.Round(deltaTime * 2000000);
                    trace.AddTime(deltaTimeScaled);

                    lastStepTime = currentTime;
                    lastStep = currentStep;
                }
            }
        }
    }
}
