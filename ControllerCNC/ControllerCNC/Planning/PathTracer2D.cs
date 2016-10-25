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
        private Vector _actualVelocity = new Vector(1000, 1000);

        private Vector _actualPosition = new Vector(0, 0);

        private List<Plan> _pathPlansX = new List<Plan>();
        private List<Plan> _pathPlansY = new List<Plan>();

        public void AppendAcceleration(Vector acceleration, double time)
        {
            var initialDeltaX = getActualDeltaX();
            var initialDeltaY = getActualDeltaY();

            var startPosition = _actualPosition;
            _actualPosition = _actualVelocity * time + 0.5 * acceleration * time * time;
            _actualVelocity = _actualVelocity + acceleration * time;
            var endPosition = _actualPosition;

            var distance = endPosition - startPosition;

            var endDeltaX = getActualDeltaX();
            var endDeltaY = getActualDeltaY();
            var nX = calculateN(initialDeltaX, acceleration.X);
            var nY = calculateN(initialDeltaY, acceleration.Y);

            addAccelerationPlan(initialDeltaX, endDeltaX, nX, distance.X, _pathPlansX);
            addAccelerationPlan(initialDeltaY, endDeltaY, nY, distance.Y, _pathPlansY);
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
                var baseDeltaExact = DriverCNC.TimeScale / velocity;
                var baseDelta = (int)(baseDeltaExact);
                var periodDenominator = UInt16.MaxValue;
                UInt16 periodNumerator = (UInt16)Math.Round((baseDeltaExact - baseDelta) * periodDenominator);
                var constantPlan = new ConstantPlan((Int16)Math.Round(distance), baseDelta, periodNumerator, periodDenominator);
                pathPlans.Add(constantPlan);
            }
        }

        private void addAccelerationPlan(double initialDelta, double endDelta, double n, double distance, List<Plan> pathPlans)
        {
            checked
            {
                if (Double.IsInfinity(n))
                    n = Int16.MaxValue;
                var accelerationPlan = new AccelerationPlan((Int16)Math.Round(distance), (int)Math.Round(initialDelta), (Int16)Math.Round(n), (int)Math.Round(endDelta));
                pathPlans.Add(accelerationPlan);
            }
        }

        private double getActualDeltaX()
        {
            if (_actualVelocity.X == 0)
                //TODO handle better
                return 20000;

            return DriverCNC.TimeScale / _actualVelocity.X;
        }

        private double getActualDeltaY()
        {
            if (_actualVelocity.Y == 0)
                //TODO handle better
                return 20000;

            return DriverCNC.TimeScale / _actualVelocity.Y;
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
                var n1 = (double)DriverCNC.TimeScale  / acceleration * DriverCNC.TimeScale / 2 / startDeltaT / startDeltaT;

                return n1;
            }
        }
    }
}
