using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class StraightLinePlanner2D
    {
        private readonly Trajectory4D _trajectory;

        private readonly Velocity _maxVelocity2D;

        private readonly Acceleration _maxAcceleration1D;

        public StraightLinePlanner2D(Trajectory4D trajectory, Velocity maxVelocity2D, Acceleration maxAcceleration1D)
        {
            _trajectory = trajectory;
            _maxVelocity2D = maxVelocity2D;
            _maxAcceleration1D = maxAcceleration1D;
        }

        public void Run(DriverCNC cnc)
        {
            Point4D lastPoint = null;
            foreach (var point in _trajectory.Points)
            {
                if (lastPoint == null)
                {
                    lastPoint = point;
                    continue;
                }

                var distanceX = point.X - lastPoint.X;
                var distanceY = point.Y - lastPoint.Y;

                var transitionTime = (long)(Math.Sqrt(distanceX * distanceX + distanceY * distanceY) / _maxVelocity2D.StepCount * _maxVelocity2D.Time);

                sendTransition2(distanceX, distanceY, transitionTime, cnc);
                lastPoint = point;
            }
        }

        private void sendTransition2(int distanceX, int distanceY, long transitionTime, DriverCNC cnc)
        {
            checked
            {
                var chunkTimeLimit = 60000;

                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkCount = 1.0 * transitionTime / chunkTimeLimit;


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

                    var stepTimeX = stepCountX == 0 ? (UInt16)0 : (UInt16)(stepsTime / Math.Abs(stepCountX));
                    var stepTimeY = stepCountY == 0 ? (UInt16)0 : (UInt16)(stepsTime / Math.Abs(stepCountY));

                    var remainderX = Math.Abs(stepCountXD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountXD));
                    var remainderY = Math.Abs(stepCountYD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountYD));

                    if (stepTimeX == 0 && stepTimeY == 0)
                    {
                        if (doneDistanceX == distanceX && doneDistanceY == distanceY)
                            break;
                        throw new NotImplementedException("Send wait signal");
                    }

                    doneTime += stepsTime;//Math.Max(Math.Abs(stepTimeX * stepCountX), Math.Abs(stepTimeY * stepCountY));

                    cnc.StepperIndex = 2;
                    cnc.SEND_Constant(stepCountX, stepTimeX, remainderX, (UInt16)Math.Abs(stepCountXD));
                    cnc.SEND_Constant(stepCountY, stepTimeY, remainderY, (UInt16)Math.Abs(stepCountYD));

                    i = i + 1 > chunkCount ? chunkCount : i + 1;
                }
            }
        }

        private void sendTransition(int distanceX, int distanceY, long transitionTime, DriverCNC cnc)
        {
            checked
            {
                var stepTimeX = distanceX == 0 ? (UInt16)30000 : (UInt16)(transitionTime / Math.Abs(distanceX));
                var stepTimeY = distanceY == 0 ? (UInt16)30000 : (UInt16)(transitionTime / Math.Abs(distanceY));


                var remainingDistanceX = distanceX;
                var remainingDistanceY = distanceY;

                while (Math.Abs(remainingDistanceX) > 0 || Math.Abs(remainingDistanceY) > 0)
                {
                    Int16 stepXCount = 0;
                    Int16 stepYCount = 0;

                    if (stepTimeX < stepTimeY)
                    {
                        stepXCount = cnc.GetStepSlice(remainingDistanceX);
                        remainingDistanceX -= stepXCount;
                        stepYCount = (Int16)(distanceY * ((distanceX - remainingDistanceX) / distanceX) - (distanceY - remainingDistanceY));
                        remainingDistanceY -= stepYCount;
                    }
                    else
                    {
                        stepYCount = cnc.GetStepSlice(remainingDistanceY);
                        remainingDistanceY -= stepYCount;
                        stepXCount = (Int16)(distanceX * ((distanceY - remainingDistanceY) / distanceY) - (distanceX - remainingDistanceX));
                        remainingDistanceX -= stepXCount;
                    }

                    cnc.StepperIndex = 2;
                    cnc.SEND_Constant(stepXCount, stepTimeX, 0, 0);
                    cnc.SEND_Constant(stepYCount, stepTimeY, 0, 0);
                }
            }
        }
    }
}
