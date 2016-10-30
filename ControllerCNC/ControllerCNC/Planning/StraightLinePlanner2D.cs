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
    class ConstantSpeedLinePlanner2D
    {
        private readonly Speed _transitionSpeed;

        public ConstantSpeedLinePlanner2D(Speed transitionSpeed)
        {
            _transitionSpeed = transitionSpeed;
        }

        /// <summary>
        /// Creates the plan.
        /// </summary>
        /// <param name="trajectory">Trajectory which plan will be created.</param>
        /// <returns>The created plan.</returns>
        public IEnumerable<InstructionCNC> CreatePlan(Trajectory4D trajectory)
        {
            var planBuilder = new PlanBuilder();
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
              
                planBuilder.AddConstantSpeedTransition2D(distanceX, distanceY, _transitionSpeed);                
                lastPoint = point;
            }

            return planBuilder.Build();
        }
    }
}
