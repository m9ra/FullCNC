using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning.TraceProfiles
{
    /// <summary>
    /// Optimizes towards reaching the target speed (but does not require it).
    /// </summary>
    class TargetPlaneSpeedOptimizer : TraceProfileBase
    {
        /// <summary>
        /// Target speed for the plane.
        /// </summary>
        private readonly Speed _targetPlaneSpeed;

        /// <summary>
        /// Acceleration for the plane.
        /// </summary>
        private readonly Acceleration _planeAcceleration;

        internal TargetPlaneSpeedOptimizer(Speed targetPlaneSpeed, Acceleration planeAcceleration)
        {
            if (targetPlaneSpeed == null)
                throw new ArgumentNullException("targetPlaneSpeed");

            if (planeAcceleration == null)
                throw new ArgumentNullException("planeAcceleration");

            _targetPlaneSpeed = targetPlaneSpeed;
            _planeAcceleration = planeAcceleration;
        }

        /// </inheritdoc>
        protected override int requiresTicks()
        {
            //no time is required - profile tries to satisfy target speed, does not enforce it
            return 0;
        }

        /// </inheritdoc>
        protected override int nextStepTicks()
        {
            var axisAcceleration = AsPlaneAxisDouble(_planeAcceleration);
            var axisTargetSpeed = AsPlaneAxis(_targetPlaneSpeed);
            var axisStartSpeed = StartSpeedAxis();

            var startDeltaT = axisStartSpeed.ToDeltaT();
            var targetDeltaT = axisTargetSpeed.ToDeltaT();

            var c0 = Math.Sqrt(2 / axisAcceleration);
            if (startDeltaT > targetDeltaT)
                //deceleration is needed
                c0 = -c0;

            var initialN = AccelerationBuilder.FindInitialN(c0, startDeltaT);
            var c0Discrete = (int)Math.Round(c0);
            var endN = AccelerationBuilder.FindEndN(c0Discrete, initialN, ProfileTickCount);
            var nextStepDelta = AccelerationBuilder.FindTargetDelta(c0Discrete, initialN, endN + 1);

            return nextStepDelta;
        }
    }
}
