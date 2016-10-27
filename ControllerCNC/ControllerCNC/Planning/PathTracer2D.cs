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

            if (Math.Sign(initialVelocity.X) * Math.Sign(endVelocity.X) < 0)
                throw new NotImplementedException();

            if (Math.Sign(initialVelocity.Y) * Math.Sign(endVelocity.Y) < 0)
                throw new NotImplementedException();


            addRampPlan(initialVelocity.X, endVelocity.X, time, distance.X, _pathPlansX);
            addRampPlan(initialVelocity.Y, endVelocity.Y, time, distance.Y, _pathPlansY);
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

                System.Diagnostics.Debug.WriteLine("PathTracer");
                System.Diagnostics.Debug.WriteLine("\tX: " + planX);
                System.Diagnostics.Debug.WriteLine("\tY: " + planY);

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

                var baseDeltaExact = Math.Abs(DriverCNC.TimeScale / velocity);
                var baseDelta = Math.Abs((int)(baseDeltaExact));
                var periodDenominator = (UInt16)Math.Abs(distanceSteps);
                UInt16 periodNumerator = (UInt16)((baseDeltaExact - baseDelta) * periodDenominator);
                var constantPlan = new ConstantPlan(distanceSteps, baseDelta, periodNumerator, periodDenominator);
                pathPlans.Add(constantPlan);
            }
        }

        private void addRampPlan(double initialSpeed, double endSpeed, double exactDuration, double distance, List<Plan> pathPlans)
        {
            var isDeceleration = Math.Abs(initialSpeed) > Math.Abs(endSpeed);
            checked
            {
                var distanceStepsAbs = (int)Math.Abs(distance);
                var profile = findAccelerationProfile(initialSpeed, endSpeed, distance, exactDuration, isDeceleration);

                var startN = isDeceleration ? -profile.StartN : profile.StartN;
                var accelerationPlan = new AccelerationPlan((Int16)distance, profile.StartDelta, startN);
                var timeDiff = Math.Abs(profile.Duration - exactDuration * DriverCNC.TimeScale);
                System.Diagnostics.Debug.WriteLine("Acceleration time diff: " + timeDiff);
                pathPlans.Add(accelerationPlan);
            }
        }

        private AccelerationProfile findAccelerationProfile(double initialSpeed, double endSpeed, double distance, double exactDuration, bool isDeceleration)
        {
            var constantSpeedPart = isDeceleration ? endSpeed : initialSpeed;
            var accelerationDistance = Math.Abs(distance - exactDuration * constantSpeedPart);
            var acceleration = accelerationDistance * 2 / exactDuration / exactDuration;
            if ((int)accelerationDistance == 0)
                throw new NotImplementedException("there is no need for acceleration - just keep speed");

            var rawC0 = DriverCNC.TimeScale * Math.Sqrt(1 / Math.Abs(acceleration));
            var c0 = rawC0;// 0.676 * rawC0;

            var distanceSteps = (int)distance;
            var durationTicks = (long)(exactDuration * DriverCNC.TimeScale);
            var minimalSpeed = isDeceleration ? endSpeed : initialSpeed;
            var deltaBoundary = 0;
            var minimalStartDeltaT = (int)(DriverCNC.TimeScale / Math.Abs(minimalSpeed));
            if (minimalStartDeltaT < 0)
                //overflow occured
                minimalStartDeltaT = int.MaxValue;
            else
                minimalStartDeltaT += deltaBoundary;

            var maximalSpeed = isDeceleration ? initialSpeed : endSpeed;
            var maximalEndDeltaT = (int)(DriverCNC.TimeScale / Math.Abs(maximalSpeed)) - deltaBoundary;

            var exactEndDelta = DriverCNC.TimeScale / endSpeed;

            var optimizationStep = 1.0;
            for (var i = 0; i < 150; ++i)
            {
                optimizationStep = optimizationStep * 0.90;
                var factor = 1.0 - optimizationStep;
                c0 = optimizeC0(c0, minimalStartDeltaT, maximalEndDeltaT, factor, distanceSteps, durationTicks, isDeceleration);
                var factor2 = 1.0 + optimizationStep;
                c0 = optimizeC0(c0, minimalStartDeltaT, maximalEndDeltaT, factor2, distanceSteps, durationTicks, isDeceleration);
            }


            var plan = new AccelerationProfile((int)c0, minimalStartDeltaT, maximalEndDeltaT, distanceSteps, durationTicks, isDeceleration);
            return plan;
        }


        private double optimizeC0(double c0, int minimalStartDeltaT, int maximalStartDeltaT, double factor, int distanceSteps, long exactDuration, bool isDeceleration)
        {
            if (distanceSteps == 0)
                return 0;

            var profile = new AccelerationProfile((int)c0, minimalStartDeltaT, maximalStartDeltaT, distanceSteps, exactDuration, isDeceleration);
            AccelerationProfile lastProfile = null;
            while (lastProfile == null || Math.Abs(lastProfile.Duration - exactDuration) > Math.Abs(profile.Duration - exactDuration))
            {
                lastProfile = profile;
                c0 = c0 * factor;
                profile = new AccelerationProfile((int)c0, minimalStartDeltaT, maximalStartDeltaT, distanceSteps, exactDuration, isDeceleration);
            }

            return lastProfile.C0;
        }
    }
}
