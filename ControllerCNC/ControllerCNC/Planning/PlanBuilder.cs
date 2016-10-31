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
    public class PlanBuilder
    {
        /// <summary>
        /// Plan which is built.
        /// </summary>
        private readonly List<InstructionCNC> _plan = new List<InstructionCNC>();

        /// <summary>
        /// Builds the plan.
        /// </summary>
        /// <returns>The plan built.</returns>
        public IEnumerable<InstructionCNC> Build()
        {
            return _plan.ToArray();
        }

        /// <summary>
        /// Adds a plan instruction for simultaneous controll of two axes.
        /// The instruction type has to be same for the axes.
        /// </summary>
        /// <param name="instructionX">instruction for the x axis.</param>
        /// <param name="instructionY">instruction for the y axis.</param>
        public void AddXY(InstructionCNC instructionX, InstructionCNC instructionY)
        {
            _plan.Add(Axes.XY(instructionX, instructionY));
        }

        /// <summary>
        /// Adds acceleration for x and y axes.
        /// </summary>
        /// <param name="accelerationProfileX">Profile for x axis acceleration.</param>
        /// <param name="accelerationProfileY">Profile for y axis acceleration.</param>
        public void AddAccelerationXY(AccelerationProfile accelerationProfileX, AccelerationProfile accelerationProfileY)
        {
            AddXY(accelerationProfileX.ToInstruction(), accelerationProfileY.ToInstruction());
        }

        /// <summary>
        /// Adds transition with specified entry, cruise and leaving speeds by RPM.
        /// </summary>
        /// <param name="stepCount">How many steps will be done.</param>
        /// <param name="startRPM">RPM at the start.</param>
        /// <param name="targetRPM">Cruise RPM.</param>
        /// <param name="endRPM">RPM at the end.</param>
        public void AddTransitionRPM(int stepCount, int startRPM, int targetRPM, int endRPM)
        {
            var startDeltaT = DeltaTFromRPM(startRPM);
            var targetDeltaT = DeltaTFromRPM(targetRPM);
            var endDeltaT = DeltaTFromRPM(endRPM);

            //SEND_Transition(stepCount, startDeltaT, targetDeltaT, endDeltaT);
            throw new NotImplementedException("Refactoring");
        }


        /// <summary>
        /// Adds 2D transition with constant speed.
        /// </summary>
        /// <param name="distanceX">Distance along X axis in steps.</param>
        /// <param name="distanceY">Distance along Y axis in steps.</param>
        /// <param name="transitionSpeed">Speed of the transition.</param>
        public void AddConstantSpeedTransitionXY(int distanceX, int distanceY, Speed transitionSpeed)
        {
            checked
            {
                var sqrt = Math.Sqrt(1.0 * distanceX * distanceX + 1.0 * distanceY * distanceY);
                var transitionTime = (long)(sqrt * transitionSpeed.Ticks / transitionSpeed.StepCount);

                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkLengthLimit = 31500;
                var chunkCount = 1.0 * Math.Max(Math.Abs(distanceX), Math.Abs(distanceY)) / chunkLengthLimit;
                chunkCount = Math.Max(1, chunkCount);


                var doneDistanceX = 0L;
                var doneDistanceY = 0L;
                var doneTime = 0.0;

                var i = Math.Min(1.0, chunkCount);
                while (Math.Abs(remainingStepsX) > 0 || Math.Abs(remainingStepsY) > 0)
                {
                    var chunkDistanceX = distanceX * i / chunkCount;
                    var chunkDistanceY = distanceY * i / chunkCount;
                    var chunkTime = transitionTime * i / chunkCount;

                    var stepCountXD = chunkDistanceX - doneDistanceX;
                    var stepCountYD = chunkDistanceY - doneDistanceY;
                    var stepsTime = chunkTime - doneTime;

                    var stepCountX = (Int16)Math.Round(stepCountXD);
                    var stepCountY = (Int16)Math.Round(stepCountYD);

                    doneDistanceX += stepCountX;
                    doneDistanceY += stepCountY;

                    //we DON'T want to round here - this way we can distribute time precisely
                    var stepTimeX = stepCountX == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountX));
                    var stepTimeY = stepCountY == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountY));

                    var timeRemainderX = Math.Abs(stepCountXD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountXD));
                    var timeRemainderY = Math.Abs(stepCountYD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountYD));

                    if (stepTimeX == 0 && stepTimeY == 0)
                    {
                        if (doneDistanceX == distanceX && doneDistanceY == distanceY)
                            break;
                        throw new NotImplementedException("Send wait signal");
                    }

                    doneTime += stepsTime;

                    var xPart = createConstant(stepCountX, stepTimeX, timeRemainderX);
                    var yPart = createConstant(stepCountY, stepTimeY, timeRemainderY);
                    AddXY(xPart, yPart);
                    i = i + 1 > chunkCount ? chunkCount : i + 1;
                }
            }
        }

        /// <summary>
        /// Adds ramped line with specified number of steps.
        /// </summary>
        /// <param name="xSteps">Number of steps along x.</param>
        /// <param name="ySteps">Numer of steps along y.</param>
        /// <param name="planeAcceleration">Acceleration used for ramping - calculated for both axis combined.</param>
        /// <param name="planeSpeedLimit">Maximal speed that could be achieved for both axis combined.</param>
        public void AddRampedLineXY(int xSteps, int ySteps, Acceleration planeAcceleration, Speed planeSpeedLimit)
        {
            if (xSteps == 0 && ySteps == 0)
                //nothing to do
                return;
            Speed speedLimitX, speedLimitY;
            DecomposeXY(xSteps, ySteps, planeSpeedLimit, out speedLimitX, out speedLimitY);

            Acceleration accelerationX, accelerationY;
            DecomposeXY(xSteps, ySteps, planeAcceleration, out accelerationX, out accelerationY);

            var accelerationProfileX = AccelerationProfile.FromTo(Speed.Zero, speedLimitX, accelerationX, xSteps / 2);
            var accelerationProfileY = AccelerationProfile.FromTo(Speed.Zero, speedLimitY, accelerationY, ySteps / 2);
            var reachedSpeedX = Speed.FromDelta(accelerationProfileX.EndDelta);
            var reachedSpeedY = Speed.FromDelta(accelerationProfileY.EndDelta);
            var reachedSpeed = ComposeXY(reachedSpeedX, reachedSpeedY);

            var decelerationProfileX = AccelerationProfile.FromTo(reachedSpeedX, Speed.Zero, accelerationX, xSteps / 2);
            var decelerationProfileY = AccelerationProfile.FromTo(reachedSpeedY, Speed.Zero, accelerationY, ySteps / 2);

            var remainingX = xSteps - accelerationProfileX.StepCount - decelerationProfileX.StepCount;
            var remainingY = ySteps - accelerationProfileY.StepCount - decelerationProfileY.StepCount;

            //send ramp
            AddAccelerationXY(accelerationProfileX, accelerationProfileY);
            AddConstantSpeedTransitionXY(remainingX, remainingY, reachedSpeed);
            AddAccelerationXY(decelerationProfileX, decelerationProfileY);
        }

        #region Acceleration calculation utilities

        /// <summary>
        /// Compose separate axes speeds into a plane speed.
        /// </summary>
        /// <param name="speedX">Speed for x axis.</param>
        /// <param name="speedY">Speed for y axis.</param>
        /// <returns>The composed speed.</returns>
        public Speed ComposeXY(Speed speedX, Speed speedY)
        {
            checked
            {
                var composedSpeed = Math.Sqrt(1.0 * speedX.StepCount * speedX.StepCount / speedX.Ticks / speedX.Ticks + 1.0 * speedY.StepCount * speedY.StepCount / speedY.Ticks / speedY.Ticks);

                var resolution = Constants.TimerFrequency * 1000;
                return new Speed((long)Math.Round(Math.Abs(composedSpeed * resolution)), resolution);
            }
        }

        /// <summary>
        /// Decomposes plane speed into separate axes speeds in a direction specified by step counts.
        /// </summary>
        /// <param name="planeSpeed">Speed within the plane</param>
        /// <param name="speedX">Output speed for x axis.</param>
        /// <param name="speedY">Output speed for y axis.</param>
        public void DecomposeXY(int stepsX, int stepsY, Speed planeSpeed, out Speed speedX, out Speed speedY)
        {
            //TODO verify/improve precision
            checked
            {
                var direction = new Vector(stepsX, stepsY);
                direction.Normalize();

                var speedVector = direction * planeSpeed.StepCount / planeSpeed.Ticks;
                var resolution = Constants.TimerFrequency;

                speedX = new Speed((long)Math.Round(Math.Abs(speedVector.X * resolution)), resolution);
                speedY = new Speed((long)Math.Round(Math.Abs(speedVector.Y * resolution)), resolution);
            }
        }

        /// <summary>
        /// Decomposes plane acceleration into separate axes accelerations in a direction specified by step counts.
        /// </summary>
        public void DecomposeXY(int stepsX, int stepsY, Acceleration planeAcceleration, out Acceleration accelerationX, out Acceleration accelerationY)
        {
            checked
            {
                Speed speedX, speedY;
                DecomposeXY(stepsX, stepsY, planeAcceleration.Speed, out speedX, out speedY);

                accelerationX = new Acceleration(speedX, planeAcceleration.Ticks);
                accelerationY = new Acceleration(speedY, planeAcceleration.Ticks);
            }
        }

        #endregion

        #region Planning related calculation utilities

        public Int16 GetStepSlice(long steps, Int16 maxSize = 30000)
        {
            maxSize = Math.Abs(maxSize);
            if (steps > 0)
                return (Int16)Math.Min(maxSize, steps);
            else
                return (Int16)Math.Max(-maxSize, steps);
        }


        public int DeltaTFromRPM(int rpm)
        {
            if (rpm == 0)
                return Constants.StartDeltaT;

            checked
            {
                var deltaT = Constants.TimerFrequency * 60 / 400 / rpm;
                return (int)deltaT;
            }
        }
        #endregion

        #region Obsolete acceleration calculation
        [Obsolete("Use correct acceleration profiles instead")]
        internal static AccelerationInstruction CalculateBoundedAcceleration(int startDeltaT, int endDeltaT, Int16 accelerationDistanceLimit, int accelerationNumerator = 1, int accelerationDenominator = 1)
        {
            checked
            {
                if (accelerationDistanceLimit == 0)
                {
                    return new AccelerationInstruction(0, 0, 0, 0, 0);
                }

                var stepSign = accelerationDistanceLimit >= 0 ? 1 : -1;
                var limit = Math.Abs(accelerationDistanceLimit);

                var startN = calculateN(startDeltaT, accelerationNumerator, accelerationDenominator);
                var endN = calculateN(endDeltaT, accelerationNumerator, accelerationDenominator);

                Int16 stepCount;
                if (startN < endN)
                {
                    //acceleration
                    stepCount = (Int16)(Math.Min(endN - startN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, startN, stepCount, accelerationNumerator, accelerationDenominator);
                    return new AccelerationInstruction((Int16)(stepCount * stepSign), startDeltaT, 0, 0, startN);
                }
                else
                {
                    //deceleration
                    stepCount = (Int16)(Math.Min(startN - endN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, (Int16)(-startN), stepCount, accelerationNumerator, accelerationDenominator);
                    return new AccelerationInstruction((Int16)(stepCount * stepSign), startDeltaT, 0, 0, (Int16)(-startN));
                }
            }
        }

        private static UInt16 calculateDeltaT(int startDeltaT, Int16 startN, Int16 stepCount, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var endN = Math.Abs(startN + stepCount);
                return (UInt16)(Constants.TimerFrequency / Math.Sqrt(2.0 * endN * (long)Constants.MaxAcceleration));
            }
        }

        private static Int16 calculateN(int startDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var n1 = (long)Constants.TimerFrequency * Constants.TimerFrequency * accelerationDenominator / 2 / startDeltaT / startDeltaT / Constants.MaxAcceleration / accelerationNumerator;
                return (Int16)Math.Max(1, n1);
            }
        }

        #endregion

        #region Private utilities

        /// <summary>
        /// Creates a plan part for constant speed segment.
        /// </summary>      
        private ConstantInstruction createConstant(Int16 stepCount, int baseTime, UInt16 timeRemainder)
        {
            checked
            {
                return new ConstantInstruction(stepCount, baseTime, timeRemainder, (UInt16)Math.Abs(stepCount));
            }
        }

        #endregion
    }
}
