using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC.Demos
{
    static class MachineTesting
    {
        public static PlanBuilder Shape4dTest()
        {
            var metricShapeThickness = 300;
            var points = ShapeDrawing.CircleToSquare();
            var projector = new PlaneProjector(metricShapeThickness, Constants.FullWireLength);
            var projectedPoints = projector.Project(points);
            var planner = new StraightLinePlanner4D(Speed.FromDeltaT(3000));
            return planner.CreateConstantPlan(new Trajectory4D(projectedPoints.As4Dstep()));
        }

        /// <summary>
        /// Draws multicross by using ramped lines.
        /// </summary>
        public static PlanBuilder AcceleratedMultiCross()
        {
            return ShapeDrawing.DrawByRampedLines(ShapeDrawing.MulticrossCoordinates);
        }

        /// <summary>
        /// Tests path tracing algorithm driven by accelerations.
        /// </summary>
        public static PlanBuilder AccelerationTest()
        {
            var speed = Constants.MaxPlaneSpeed;
            var acceleration = Constants.MaxPlaneAcceleration;

            var tracer = new PathTracer2D();
            var maxAcceleration = 1 * 400;

            var direction1 = new Vector(-400, -200);
            var direction2 = new Vector(-20, -400);
            direction1.Normalize();
            direction2.Normalize();

            tracer.AppendAcceleration(direction1 * maxAcceleration, 2);
            tracer.AppendAcceleration(direction1 * maxAcceleration, 2);
            tracer.AppendAcceleration(-direction1 * maxAcceleration, 2);
            tracer.Continue(2);
            tracer.AppendAcceleration(direction2 * maxAcceleration, 2);
            tracer.AppendAcceleration(-2 * direction1 * maxAcceleration, 2);
            tracer.AppendAcceleration(-direction2 * maxAcceleration, 2);
            tracer.Continue(2);
            return tracer.FillBuilder();
        }

        /// <summary>
        /// Traverses axis in a full range several times.
        /// </summary>
        public static PlanBuilder BackAndForwardAxisTraversal()
        {
            var traverseDelta = 400;
            var builder = new PlanBuilder();

            for (var i = 0; i < 30; ++i)
            {
                var distance = Constants.MaxStepsX;
                var acceleration = Coord2DController.CreateAcceleration(Constants.StartDeltaT, traverseDelta)[0];
                var deceleration = Coord2DController.CreateAcceleration(traverseDelta, Constants.StartDeltaT)[0];
                distance -= Math.Abs(acceleration.StepCount);
                distance -= Math.Abs(deceleration.StepCount);

                if (i % 2 == 0)
                {
                    builder.AddXY(acceleration, null);
                    builder.AddConstantSpeedTransitionXY(distance, 0, Speed.FromDeltaT(traverseDelta));
                    builder.AddXY(deceleration, null);
                }
                else
                {
                    builder.AddXY(acceleration.WithReversedDirection(), null);
                    builder.AddConstantSpeedTransitionXY(-distance, 0, Speed.FromDeltaT(traverseDelta));
                    builder.AddXY(deceleration.WithReversedDirection(), null);
                }
            }

            builder.ChangeXYtoUV();
            return builder;
        }

        /// <summary>
        /// Demo with several acceleration ramps.
        /// </summary>
        public static PlanBuilder Ramping()
        {
            var builder = new PlanBuilder();

            builder.AddTransitionRPM(-400 * 10, 0, 1000, 500);
            builder.AddTransitionRPM(-400 * 10, 500, 500, 500);
            builder.AddTransitionRPM(-400 * 20, 500, 1500, 0);
            return builder;
        }

        /// <summary>
        /// Demo with a single revolution interrupted several times.
        /// </summary>
        public static PlanBuilder InterruptedRevolution()
        {
            var plan = new PlanBuilder();
            var segmentation = 100;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                plan.AddRampedLineXY(segmentation, segmentation, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            }

            return plan;
        }

        /// <summary>
        /// A single revolution with a lot of forward/backward direction changes.
        /// </summary>
        /// <returns></returns>
        public static PlanBuilder BackAndForwardRevolution()
        {
            var plan = new PlanBuilder();
            var overShoot = 100;
            var segmentation = 4;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                plan.AddRampedLineXY(-overShoot, -overShoot, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
                plan.AddRampedLineXY(segmentation + overShoot, segmentation + overShoot, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            }

            return plan;
        }
    }
}
