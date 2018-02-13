using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using System.Numerics;
using System.Diagnostics;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    /// <summary>
    /// Simple planner for constant speed transitions between 2D coordinates.
    /// </summary>
    class StraightLinePlanner
    {
        /// <summary>
        /// The desired speed of transition between points.
        /// </summary>
        private readonly Speed _transitionSpeed;

        public StraightLinePlanner(Speed transitionSpeed)
        {
            _transitionSpeed = transitionSpeed;
        }

        /// <summary>
        /// Creates the plan connecting coordinates constant speed without acceleration.
        /// </summary>
        /// <param name="trajectory">Trajectory which plan will be created.</param>
        /// <returns>The created plan.</returns>
        public PlanBuilder CreateConstantPlan(Trajectory4D trajectory)
        {
            var planBuilder = new PlanBuilder();

            iterateDistances(trajectory, (p, x, y) => planBuilder.AddConstantSpeedTransitionXY(x, y, _transitionSpeed));
            return planBuilder;
        }

        /// <summary>
        /// Creates the plan connecting coordinates by ramped lines.
        /// </summary>
        /// <param name="trajectory">Trajectory which plan will be created.</param>
        /// <returns>The created plan.</returns>
        public PlanBuilder CreateRampedPlan(Trajectory4D trajectory)
        {
            var planBuilder = new PlanBuilder();

            iterateDistances(trajectory, (p, x, y) => planBuilder.AddRampedLineXY(x, y, Configuration.MaxPlaneAcceleration, Configuration.MaxPlaneSpeed));
            return planBuilder;
        }

        /// <summary>
        /// Creates the plan connecting coordinates by lines with continuous speed.
        /// </summary>
        /// <param name="trajectory">Trajectory which plan will be created.</param>
        /// <returns>The created plan.</returns>
        public PlanBuilder CreateContinuousPlan(Trajectory4D trajectory)
        {
            var pointSpeeds = createPointSpeeds(trajectory.Points);

            var planBuilder = new PlanBuilder();

            var currentSpeed = Speed.Zero;
            iterateDistances(trajectory, (p, x, y) =>
            {
                var targetSpeedF = pointSpeeds[p];
                var targetSpeed = new Speed((int)(targetSpeedF), Configuration.TimerFrequency);
                planBuilder.AddLineXY(x, y, currentSpeed, Configuration.MaxPlaneAcceleration, targetSpeed);
                currentSpeed = targetSpeed;
            });

            return planBuilder;
        }

        private void iterateDistances(Trajectory4D trajectory, Action<Point4Dstep, int, int> planner)
        {
            Point4Dstep lastPoint = null;
            foreach (var point in trajectory.Points)
            {
                if (lastPoint == null)
                {
                    lastPoint = point;
                    continue;
                }

                var distanceX = point.X - lastPoint.X;
                var distanceY = point.Y - lastPoint.Y;

                planner(point, distanceX, distanceY);
                lastPoint = point;
            }
        }

        private Dictionary<Point4Dstep, double> createPointSpeeds(IEnumerable<Point4Dstep> points)
        {
            var junctionLimits = createJunctionLimits(points);

            var calculationPlan = new PlanBuilder();
            var speedLimits = new Dictionary<Point4Dstep, double>();
            var currentSpeedF = 1.0 * Configuration.TimerFrequency / Configuration.StartDeltaT;
            var stepAcceleration = 1;
            Point4Dstep previousPoint = null;
            foreach (var point in points.Reverse())
            {
                var junctionLimitF = junctionLimits[point];

                var newSpeedF = currentSpeedF + stepAcceleration;
                if (previousPoint != null)
                {
                    var currentSpeed = new Speed((int)(currentSpeedF), Configuration.TimerFrequency);
                    var junctionSpeed = new Speed((int)(junctionLimitF), Configuration.TimerFrequency);
                    var stepsX = previousPoint.X - point.X;
                    var stepsY = previousPoint.Y - point.Y;
                    var newSpeed = calculationPlan.AddLineXY(stepsX, stepsY, currentSpeed, Configuration.MaxPlaneAcceleration, junctionSpeed);
                    newSpeedF = 1.0 * newSpeed.StepCount / newSpeed.Ticks * Configuration.TimerFrequency;
                }


                currentSpeedF = Math.Min(newSpeedF, junctionLimitF);

                if (double.IsNaN(currentSpeedF))
                    throw new NotSupportedException("invalid computation");
                speedLimits[point] = currentSpeedF;
                previousPoint = point;
            }

            previousPoint = null;
            currentSpeedF = 1.0 * Configuration.TimerFrequency / Configuration.StartDeltaT;
            foreach (var point in points)
            {
                var speedLimitF = speedLimits[point];
                var newSpeedF = currentSpeedF + stepAcceleration;
                if (previousPoint != null)
                {
                    var currentSpeed = new Speed((int)(currentSpeedF), Configuration.TimerFrequency);
                    var limitSpeed = new Speed((int)(speedLimitF), Configuration.TimerFrequency);
                    var stepsX = previousPoint.X - point.X;
                    var stepsY = previousPoint.Y - point.Y;
                    var newSpeed = calculationPlan.AddLineXY(stepsX, stepsY, currentSpeed, Configuration.MaxPlaneAcceleration, limitSpeed);
                    newSpeedF = 1.0 * newSpeed.StepCount / newSpeed.Ticks * Configuration.TimerFrequency;
                }
                currentSpeedF = Math.Min(newSpeedF, speedLimitF);
                speedLimits[point] = currentSpeedF;
                previousPoint = point;
                //Debug.WriteLine(currentVelocity);
            }

            return speedLimits;
        }

        private Dictionary<Point4Dstep, double> createJunctionLimits(IEnumerable<Point4Dstep> points)
        {
            var startVelocity = 1.0 * Configuration.TimerFrequency / Configuration.StartDeltaT;
            var maxVelocity = 1.0 * Configuration.TimerFrequency / Configuration.FastestDeltaT;
            var aMax = 1.0 * Configuration.MaxAcceleration / Configuration.TimerFrequency / Configuration.TimerFrequency;

            var junctionLimits = new Dictionary<Point4Dstep, double>();
            var pointsArrayRev = points.Reverse().ToArray();
            junctionLimits[pointsArrayRev.First()] = startVelocity;
            junctionLimits[pointsArrayRev.Last()] = startVelocity;

            for (var i = 1; i < pointsArrayRev.Length - 1; ++i)
            {
                var previousPoint = pointsArrayRev[i - 1];
                var currentPoint = pointsArrayRev[i];
                var nextPoint = pointsArrayRev[i + 1];

                //my angle based vJunction
                var angle = getTheta(previousPoint, currentPoint, nextPoint);
                while (angle < 0)
                    angle += 360;

                while (angle > 360)
                    angle -= 360;

                if (angle > 180)
                    angle = 360 - angle;

                if (angle < 0 || angle > 180)
                    throw new NotSupportedException("Error in normalization.");

                var velocityDiff = maxVelocity - startVelocity;
                var vJunction = (Math.Max(90, angle) / 180 - 0.5) * 2 * velocityDiff + startVelocity;

                if (double.IsNaN(vJunction))
                    throw new NotSupportedException("Invalid computation.");

                junctionLimits[currentPoint] = vJunction;
            }
            return junctionLimits;
        }

        private double getTheta(Point4Dstep point1, Point4Dstep point2, Point4Dstep point3)
        {
            var Ax = 1.0 * point2.X - point1.X;
            var Ay = 1.0 * point2.Y - point1.Y;
            var A = new Vector(Ax, Ay);

            var Bx = 1.0 * point2.X - point3.X;
            var By = 1.0 * point2.Y - point3.Y;
            var B = new Vector(Bx, By);

            return Vector.AngleBetween(A, B);
        }
    }
}
