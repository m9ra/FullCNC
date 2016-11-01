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

            iterateDistances(trajectory, (x, y) => planBuilder.AddConstantSpeedTransitionXY(x, y, _transitionSpeed));
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

            iterateDistances(trajectory, (x, y) => planBuilder.AddRampedLineXY(x, y, Constants.MaxPlaneAcceleration, _transitionSpeed));
            return planBuilder;
        }

        private void iterateDistances(Trajectory4D trajectory, Action<int, int> planner)
        {
            Point4D lastPoint = null;
            foreach (var point in trajectory.Points)
            {
                if (lastPoint == null)
                {
                    lastPoint = point;
                    continue;
                }

                var distanceX = point.X - lastPoint.X;
                var distanceY = point.Y - lastPoint.Y;

                planner(distanceX, distanceY);
                lastPoint = point;
            }
        }
    }
}
