using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using ControllerCNC.Machine;
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

        private List<StepInstrution> _pathPlansX = new List<StepInstrution>();
        private List<StepInstrution> _pathPlansY = new List<StepInstrution>();

        public void AppendAcceleration(Vector acceleration, double time)
        {
            var tickCount = time * Constants.TimerFrequency;
            if ((int)tickCount <= 0)
                //nothing to add here
                return;

            var initialVelocity = _actualVelocity;

            var startPosition = _actualPosition;
            var newPosition = _actualPosition + _actualVelocity * time + 0.5 * acceleration * time * time;
            var newVelocity = _actualVelocity + acceleration * time;

            var distance = newPosition - startPosition;

            if (Math.Sign(initialVelocity.X) * Math.Sign(newVelocity.X) < 0)
            {
                var stopTime = Math.Abs(initialVelocity.X / acceleration.X);
                if (stopTime * Constants.TimerFrequency >= 1)
                {
                    AppendAcceleration(acceleration, stopTime);
                    AppendAcceleration(acceleration, time - stopTime);
                    return;
                }
            }

            if (Math.Sign(initialVelocity.Y) * Math.Sign(newVelocity.Y) < 0)
            {
                var stopTime = Math.Abs(initialVelocity.Y / acceleration.Y);
                if (stopTime * Constants.TimerFrequency >= 1)
                {
                    AppendAcceleration(acceleration, stopTime);
                    AppendAcceleration(acceleration, time - stopTime);
                    return;
                }
            }

            _actualPosition = newPosition;
            _actualVelocity = newVelocity;
            //TODO increase precision with integer clipping
            addRampPlan(initialVelocity.X, newVelocity.X, time, distance.X, _pathPlansX);
            addRampPlan(initialVelocity.Y, newVelocity.Y, time, distance.Y, _pathPlansY);
        }


        public void Continue(double time)
        {
            var startPosition = _actualPosition;
            _actualPosition = _actualPosition + _actualVelocity * time;

            var endPosition = _actualPosition;
            var distance = endPosition - startPosition;
            var tickCount = (int)(Constants.TimerFrequency * time);
            addConstantPlan(tickCount, distance.X, _pathPlansX);
            addConstantPlan(tickCount, distance.Y, _pathPlansY);
        }

        internal PlanBuilder FillBuilder()
        {
            var builder = new PlanBuilder();
            for (var i = 0; i < _pathPlansX.Count; ++i)
            {
                var planX = _pathPlansX[i];
                var planY = _pathPlansY[i];

                System.Diagnostics.Debug.WriteLine("PathTracer");
                System.Diagnostics.Debug.WriteLine("\tX: " + planX);
                System.Diagnostics.Debug.WriteLine("\tY: " + planY);

                builder.AddXY(planX, planY);
            }

            return builder;
        }

        private void addConstantPlan(int tickCount, double distance, List<StepInstrution> pathPlans)
        {
            checked
            {
                //fraction is clipped because period can be used for remainder
                var stepCount = (Int16)distance;
                if (stepCount == 0)
                {
                    pathPlans.Add(new ConstantInstruction(0, 0, 0));
                    return;
                }
                checked
                {
                    var baseDeltaExact = Math.Abs(tickCount / stepCount);
                    var baseDelta = Math.Abs((int)(baseDeltaExact));
                    var tickRemainder = (UInt16)(tickCount - Math.Abs(stepCount) * baseDelta);
                    var constantPlan = new ConstantInstruction(stepCount, baseDelta, tickRemainder);
                    pathPlans.Add(constantPlan);
                }
            }
        }

        private void addRampPlan(double initialSpeed, double endSpeed, double exactDuration, double distance, List<StepInstrution> pathPlans)
        {
            checked
            {
                var profile = AccelerationBuilder.FromTo(initialSpeed, endSpeed, (int)Math.Round(distance), exactDuration);
                var timeDiff = Math.Abs(profile.TotalTickCount - exactDuration * Constants.TimerFrequency);
                System.Diagnostics.Debug.WriteLine("Acceleration time diff: " + timeDiff);
                System.Diagnostics.Debug.WriteLine("\t" + profile);
                pathPlans.Add(profile.ToInstruction());
            }
        }
    }
}
