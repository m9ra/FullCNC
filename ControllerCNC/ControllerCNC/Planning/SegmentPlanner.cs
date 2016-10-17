using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    /// <summary>
    /// Planner using linear segments (acceleration/deceleration/constant) for planning.
    /// </summary>
    class SegmentPlanner
    {
        /// <summary>
        /// Resulting plan in form of segments.
        /// </summary>
        private readonly List<SegmentPlanBuilder1D[]> _segments = new List<SegmentPlanBuilder1D[]>();

        public SegmentPlanner(Trajectory4D trajectory, Velocity maxVelocity4D, Acceleration maxAcceleration1D)
        {
            initializeBuilders(trajectory, maxVelocity4D);

            calculateLeadingAccelerations(maxAcceleration1D);
            calculateTrailingAccelerations(maxAcceleration1D);
        }

        /// <summary>
        /// Calculates accelerations at segment ends - counts with limit posed by following segments.
        /// </summary>
        /// <param name="maxAcceleration1D">Maximal acceleration allowed.</param>
        private void calculateTrailingAccelerations(Acceleration maxAcceleration1D)
        {
            foreach (var segment in _segments)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Calculates accelerations at segment starts.
        /// </summary>
        /// <param name="maxAcceleration1D">Maximal acceleration allowed.</param>
        private void calculateLeadingAccelerations(Acceleration maxAcceleration1D)
        {
            var lastVelocities = createZeroVelocities();
            foreach (var segment in _segments)
            {
               /* var deltaVelocities = calculateDeltaVelocities(lastVelocities, segment.Select(s => s.MaxVelocity));
                var accelerations = calculateSyncedAccelerations(deltaVelocities);

                //set segment builders accelerations
                for (var i = 0; i < segment.Length; ++i)
                {
                    segment[i].SetLeadingAcceleration(accelerations[i]);
                }*/
            }
        }

        /// <summary>
        /// Creates velocities with zero value in every dimension.
        /// </summary>
        /// <returns>The created velocities.</returns>
        private Velocity[] createZeroVelocities()
        {
            var lastVelocities = new Velocity[]{
                Velocity.Zero,
                Velocity.Zero,
                Velocity.Zero,
                Velocity.Zero,
            };
            return lastVelocities;
        }

        /// <summary>
        /// Initialize builders along the whole trajectory.
        /// </summary>
        /// <param name="trajectory">Trajectory to be planned.</param>
        /// <param name="desiredVelocity4D">Desired speed for all the dimensions combined</param>
        private void initializeBuilders(Trajectory4D trajectory, Velocity desiredVelocity4D)
        {
            Point4D currentPoint = null;
            foreach (var nextPoint in trajectory.Points)
            {
                if (currentPoint == null)
                {
                    //we are at the trajectory begining
                    currentPoint = nextPoint;
                    continue;
                }

                var builders = new List<SegmentPlanBuilder1D>();
                var velocities = marginalVelocities(currentPoint, nextPoint, desiredVelocity4D);
                builders.Add(createBuilder(nextPoint.U - currentPoint.U, velocities[0]));
                builders.Add(createBuilder(nextPoint.V - currentPoint.V, velocities[1]));
                builders.Add(createBuilder(nextPoint.X - currentPoint.X, velocities[2]));
                builders.Add(createBuilder(nextPoint.Y - currentPoint.Y, velocities[3]));

                _segments.Add(builders.ToArray());

                currentPoint = nextPoint;
            }
        }

        /// <summary>
        /// Creates builder for segment of stepCount steps which should be done with desired velocity.
        /// </summary>
        /// <param name="stepCount">Number of steps for the segment.</param>
        /// <param name="desiredVelocity">The ideal velocity for the segment.</param>
        /// <returns>The created builder.</returns>
        private SegmentPlanBuilder1D createBuilder(int stepCount, Velocity desiredVelocity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calculates velocities along separte dimensions.
        /// </summary>
        /// <param name="start">Start of the movement.</param>
        /// <param name="end">End of the movement.</param>
        /// <param name="velocity4D">The velocity accross all dimensions.</param>
        /// <returns>The marginal velocities.</returns>
        private Velocity[] marginalVelocities(Point4D start, Point4D end, Velocity velocity4D)
        {
            throw new NotImplementedException();
        }
    }
}
