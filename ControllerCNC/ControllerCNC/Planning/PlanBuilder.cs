using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class PlanBuilder
    {
        /// <summary>
        /// Plan which is built.
        /// </summary>
        private readonly List<InstructionCNC> _plan = new List<InstructionCNC>();


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

        /// <summary>
        /// Builds the plan.
        /// </summary>
        /// <returns>The plan built.</returns>
        public IEnumerable<InstructionCNC> Build()
        {
            return _plan.ToArray();
        }

        /// <summary>
        /// Adds a plan part for simultaneous controll of two axes.
        /// The part type has to be same for the axes.
        /// </summary>
        /// <param name="partX">Part for the x axis.</param>
        /// <param name="partY">Part for the y axis.</param>
        public void Add2D(InstructionCNC partX, InstructionCNC partY)
        {
            if (partX.GetType() != partY.GetType())
                throw new NotSupportedException("Part types has to be the same");

            throw new NotImplementedException("Join the parts together.");
        }


        internal void SEND_TransitionRPM(int stepCount, int startRPM, int targetRPM, int endRPM)
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
        public void AddConstantSpeedTransition2D(int distanceX, int distanceY, Speed transitionSpeed)
        {
            checked
            {
                var transitionTime = (long)(Math.Sqrt(distanceX * distanceX + distanceY * distanceY) * transitionSpeed.Ticks / transitionSpeed.StepCount);

                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkLengthLimit = 35500;
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
                    Add2D(xPart, yPart);
                    i = i + 1 > chunkCount ? chunkCount : i + 1;
                }
            }
        }

        /// <summary>
        /// Adds ramped line with specified number of steps.
        /// </summary>
        /// <param name="xSteps">Number of steps along x.</param>
        /// <param name="ySteps">Numer of steps along y.</param>
        /// <param name="acceleration">Acceleration used for ramping.</param>
        /// <param name="speedLimit">Maximal speed that could be achieved.</param>
        public void AddRampedLine2D(int xSteps, int ySteps, Acceleration acceleration, Speed speedLimit)
        {
            throw new NotImplementedException();
        }

        #region Obsolete accelertion calculation
        [Obsolete("Use correct acceleration profiles instead")]
        public static AccelerationInstruction CalculateBoundedAcceleration(int startDeltaT, int endDeltaT, Int16 accelerationDistanceLimit, int accelerationNumerator = 1, int accelerationDenominator = 1)
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
