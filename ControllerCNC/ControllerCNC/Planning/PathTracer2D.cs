using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    public class PathTracer2D
    {
        /// <summary>
        /// Velocity at end of the planned path.
        /// </summary>
        private Vector _actualVelocity = new Vector(0, 0);

        private Vector _actualPosition = new Vector(0, 0);

        private List<Plan> _pathPlansX = new List<Plan>();
        private List<Plan> _pathPlansY = new List<Plan>();

        public void AppendAcceleration(Vector acceleration, double time)
        {
            var initialVelocity = _actualVelocity;

            var startPosition = _actualPosition;
            _actualPosition = _actualPosition + _actualVelocity * time + 0.5 * acceleration * time * time;
            _actualVelocity = _actualVelocity + acceleration * time;
            var endPosition = _actualPosition;

            var distance = endPosition - startPosition;
            var endVelocity = _actualVelocity;

            addAccelerationPlan(initialVelocity.X, endVelocity.X, time, distance.X, _pathPlansX);
            addAccelerationPlan(initialVelocity.Y, endVelocity.Y, time, distance.Y, _pathPlansY);
        }


        public void Continue(double time)
        {
            var startPosition = _actualPosition;
            _actualPosition = _actualPosition + _actualVelocity * time;

            var endPosition = _actualPosition;
            var distance = endPosition - startPosition;

            addConstantPlan(_actualVelocity.X, distance.X, _pathPlansX);
            addConstantPlan(_actualVelocity.Y, distance.Y, _pathPlansY);
        }

        internal void Execute(DriverCNC cnc)
        {
            for (var i = 0; i < _pathPlansX.Count; ++i)
            {
                var planX = _pathPlansX[i];
                var planY = _pathPlansY[i];

                cnc.StepperIndex = 2;
                //TODO add polymorphism
                if (planX is AccelerationPlan)
                {
                    cnc.SEND((AccelerationPlan)planX);
                    cnc.SEND((AccelerationPlan)planY);
                }
                else
                {
                    cnc.SEND((ConstantPlan)planX);
                    cnc.SEND((ConstantPlan)planY);
                }
            }
        }

        private void addConstantPlan(double velocity, double distance, List<Plan> pathPlans)
        {
            checked
            {
                //fraction is clipped because period can be used for remainder
                var distanceSteps = (Int16)distance;
                if (distanceSteps == 0)
                {
                    pathPlans.Add(new ConstantPlan(0, 0, 0, 0));
                    return;
                }

                var baseDeltaExact = DriverCNC.TimeScale / velocity;
                var baseDelta = (int)(baseDeltaExact);
                var periodDenominator = UInt16.MaxValue;
                UInt16 periodNumerator = (UInt16)Math.Round((baseDeltaExact - baseDelta) * periodDenominator);
                var constantPlan = new ConstantPlan(distanceSteps, baseDelta, periodNumerator, periodDenominator);
                pathPlans.Add(constantPlan);
            }
        }

        private void addAccelerationPlan(double initialSpeed, double endSpeed, double exactDuration, double distance, List<Plan> pathPlans)
        {
            //TODO improve signature - acceleration vs endSpeed redundancy (we need to decide which one is more accurate)
            checked
            {
                var c0 = findC0(initialSpeed, distance, exactDuration);
                var startN = FindAccelerationN(c0, initialSpeed);

                var distanceSteps = (Int16)distance;
                var startDelta = findDelta(c0, startN);
                var endDelta = findDelta(c0, startN + distanceSteps);
                var afterEndDelta = findDelta(c0, startN + distanceSteps+1);
                var exactEndDelta = DriverCNC.TimeScale / endSpeed;

                var realDuration = calculateAccelerationDuration(c0, startN, startN + distanceSteps);


                var accelerationPlan = new AccelerationPlan(distanceSteps, startDelta, startN, endDelta);
                pathPlans.Add(accelerationPlan);
            }
        }

        private double findC0(double initialSpeed, double distance, double exactDuration)
        {
            var accelerationDistance = distance - exactDuration * initialSpeed;
            var acceleration = accelerationDistance * 2 / exactDuration / exactDuration;

            if (acceleration < 0)
                throw new NotImplementedException();

            var rawC0 = DriverCNC.TimeScale * Math.Sqrt(1 / acceleration);
            var c0 = 0.676 * rawC0;

            var distanceSteps = (int)distance;
            var durationTicks = (long)(exactDuration * DriverCNC.TimeScale);

            var optimizationStep = 1.0;
            for (var i = 0; i < 7; ++i)
            {
                optimizationStep /= 16;
                c0 = optimizeC0(c0, optimizationStep, initialSpeed, distanceSteps, durationTicks);
            }

            return c0;
        }

        private double optimizeC0(double c0, double optimizationStep, double initialSpeed, int distanceSteps, long exactDuration)
        {
            if (distanceSteps == 0)
                return 0;
            var startN = FindAccelerationN(c0, initialSpeed);
            var realDuration = calculateAccelerationDuration(c0, startN, startN + distanceSteps);
            var optimizationDirection = realDuration > exactDuration;

            var factor = optimizationDirection ? 1.0 - optimizationStep : 1.0 + optimizationStep;

            while (realDuration > exactDuration == optimizationDirection)
            {
                c0 = c0 * factor;
                startN = FindAccelerationN(c0, initialSpeed);
                realDuration = calculateAccelerationDuration(c0, startN, startN + distanceSteps);
            }

            return c0;
        }

        internal static int FindAccelerationN(double c0, double speed)
        {
            //TODO we would like to have exact analytic formula for this
            //for now we will be OK with this iterative approach (cloning CncMachine computation)
            //NOTE we now the cn=c0*(sqrt(n+1)-sqrt(n)) exact formula, however, it could differ from this integer approx.

            var targetDelta = DriverCNC.TimeScale / speed;
            var currentDelta = (int)c0;

            var currentN = 0;
            var currentDeltaBuffer2 = 0;
            while (currentDelta > targetDelta)
            {
                ++currentN;
                currentDeltaBuffer2 += currentDelta * 2;
                while (currentDeltaBuffer2 >= 4 * currentN + 1)
                {
                    currentDeltaBuffer2 -= 4 * currentN + 1;
                    --currentDelta;
                }
            }
            return currentN;
        }

        private long calculateAccelerationDuration(double c0, int startN, int endN)
        {
            var currentDelta = (int)c0;
            var currentDeltaBuffer2 = 10;
            var currentTime = 0L;
            for (var currentN = 1; currentN <= endN; ++currentN)
            {
                currentDeltaBuffer2 += currentDelta * 2;
                while (currentDeltaBuffer2 >= 4 * currentN + 1)
                {
                    currentDeltaBuffer2 -= 4 * currentN + 1;
                    --currentDelta;
                }

                if (currentN > startN)
                    currentTime += currentDelta;
            }
            //System.Diagnostics.Debug.WriteLine(currentTime);
            return currentTime;
        }

        private int findDelta(double c0, int stepCount)
        {
            //TODO we would like to have exact analytic formula for this
            //for now we will be OK with this iterative approach (cloning CncMachine computation)
            //NOTE we now the cn=c0*(sqrt(n+1)-sqrt(n)) exact formula, however, it could differ from this integer approx.

            var currentDelta = (int)c0;
            var currentDeltaBuffer2 = 0;
            for (var currentN = 1; currentN <= stepCount; ++currentN)
            {
                currentDeltaBuffer2 += currentDelta * 2;
                while (currentDeltaBuffer2 >= 4 * currentN + 1)
                {
                    currentDeltaBuffer2 -= 4 * currentN + 1;
                    --currentDelta;
                }
            }
            return currentDelta;
        }

        private static double calculateSteps(double startDeltaT, double endDeltaT, double acceleration)
        {
            var n1 = calculateN(startDeltaT, acceleration);
            var n2 = calculateN(endDeltaT, acceleration);

            return n2 - n1;
        }

        private static double calculateN(double startDeltaT, double acceleration)
        {
            checked
            {
                var n1 = (double)DriverCNC.TimeScale / acceleration * DriverCNC.TimeScale / 2 / startDeltaT / startDeltaT;

                return n1;
            }
        }
    }
}
