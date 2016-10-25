using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using System.Numerics;
using System.Diagnostics;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class StraightLinePlanner2D
    {
        private readonly Trajectory4D _trajectory;

        private readonly Dictionary<Point4D, double> _pointSpeed = new Dictionary<Point4D, double>();

        private readonly Velocity _maxVelocity2D;

        private readonly Acceleration _maxAcceleration1D;

        public StraightLinePlanner2D(Trajectory4D trajectory, Velocity maxVelocity2D, Acceleration maxAcceleration1D)
        {
            _trajectory = trajectory;
            _maxVelocity2D = maxVelocity2D;
            _maxAcceleration1D = maxAcceleration1D;

            //   var smoothedPoints = smooth(_trajectory.Points);
        }

        private IEnumerable<Point4D> smooth(IEnumerable<Point4D> points)
        {
            Point4D lastPoint = null;
            foreach (var point in points)
            {
                if (lastPoint == null)
                {
                    lastPoint = point;
                    continue;
                }


            }

            throw new NotImplementedException();
        }

        private Dictionary<Point4D, double> createPointVelocities(IEnumerable<Point4D> points, DriverCNC cnc)
        {
            var junctionLimits = createJunctionLimits(points, cnc);

            var velocityLimits = new Dictionary<Point4D, double>();
            var currentVelocity = 1.0 * DriverCNC.TimeScale / cnc.StartDeltaT;
            var stepAcceleration = 1;
            foreach (var point in points.Reverse())
            {
                var junctionLimit = junctionLimits[point];

                var newVelocity = currentVelocity + stepAcceleration;
                currentVelocity = Math.Min(newVelocity, junctionLimit);

                if (double.IsNaN(currentVelocity))
                    throw new NotSupportedException("invalid computation");
                velocityLimits[point] = currentVelocity;
            }

            currentVelocity = 1.0 * DriverCNC.TimeScale / cnc.StartDeltaT;
            foreach (var point in points)
            {
                var velocityLimit = velocityLimits[point];
                var newVelocity = currentVelocity + stepAcceleration;
                currentVelocity = Math.Min(newVelocity, velocityLimit);

                velocityLimits[point] = currentVelocity;

                //Debug.WriteLine(currentVelocity);
            }

            return velocityLimits;
        }

        private Dictionary<Point4D, double> createJunctionLimits(IEnumerable<Point4D> points, DriverCNC cnc)
        {
            var startVelocity = 1.0 * DriverCNC.TimeScale / cnc.StartDeltaT;
            var maxVelocity = 1.0 * DriverCNC.TimeScale / cnc.FastestDeltaT;
            var aMax = 1.0 * DriverCNC.MaxAcceleration / DriverCNC.TimeScale / DriverCNC.TimeScale;

            var junctionLimits = new Dictionary<Point4D, double>();
            var pointsArrayRev = points.Reverse().ToArray();
            junctionLimits[pointsArrayRev.First()] = startVelocity;
            junctionLimits[pointsArrayRev.Last()] = startVelocity;

            for (var i = 1; i < pointsArrayRev.Length - 1; ++i)
            {
                var previousPoint = pointsArrayRev[i - 1];
                var currentPoint = pointsArrayRev[i];
                var nextPoint = pointsArrayRev[i + 1];

                /*   previousPoint = new Point4D(0, 0, 10, -100);
                   currentPoint = new Point4D(0, 0, 0, 0);
                   nextPoint = new Point4D(0, 0, 0, 100);*/

                /*    var delta = 50;
                    var theta = 2 * Math.PI - getTheta(previousPoint, currentPoint, nextPoint);
                    var thetaAngl = theta * 180 / Math.PI;
                    var cosTheta = Math.Cos(theta);
                    var sinThetaHalf = Math.Sqrt((1 - cosTheta) / 2);
                    var R = delta * (sinThetaHalf / (1 - sinThetaHalf));

                    var vJunction = Math.Sqrt(aMax * R);
                    var vDesired = 1.0 * _maxVelocity2D.StepCount / _maxVelocity2D.Time;

                    if (double.IsNaN(vJunction))
                        vJunction = startVelocity;

                    vJunction = Math.Max(vJunction, startVelocity);
                    vJunction = Math.Min(vDesired, vJunction);*/

                //my angle based vJunction
                var angle = getTheta(previousPoint, currentPoint, nextPoint);
                while (angle < 0)
                    angle += 360;

                while (angle > 360)
                    angle -= 360;

                if (angle > 180)
                    angle = 360 - angle;

                if (angle < 0 || angle > 180)
                    throw new NotSupportedException("Normalize");
                var velocityDiff = maxVelocity - startVelocity;
                var vJunction = (Math.Max(90, angle) / 180 - 0.5) * 2 * velocityDiff + startVelocity;

                if (double.IsNaN(vJunction))
                    throw new NotSupportedException("invalid computation");

                junctionLimits[currentPoint] = vJunction;
            }
            return junctionLimits;
        }

        private double getTheta(Point4D point1, Point4D point2, Point4D point3)
        {
            var Ax = 1.0 * point2.X - point1.X;
            var Ay = 1.0 * point2.Y - point1.Y;
            var A = new Vector(Ax, Ay);

            var Bx = 1.0 * point2.X - point3.X;
            var By = 1.0 * point2.Y - point3.Y;
            var B = new Vector(Bx, By);

            return Vector.AngleBetween(A, B);
        }

        public void Run(DriverCNC cnc)
        {
            var velocities = createPointVelocities(_trajectory.Points, cnc);

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

                var transitionTime = (long)(Math.Sqrt(distanceX * distanceX + distanceY * distanceY) * DriverCNC.TimeScale / velocities[point]);
                var acceleration = velocities[point] - velocities[lastPoint];

                SendTransition2(distanceX, distanceY, transitionTime, cnc);
                //AcceleratedTransition(distanceX, distanceY, cnc);
                lastPoint = point;
            }
        }

        public static void AcceleratedTransition(int distanceX, int distanceY, DriverCNC cnc)
        {
            checked
            {
                var direction = new Vector(distanceX, distanceY);
                direction.Normalize();
                var acceleration = direction * DriverCNC.MaxAcceleration / 10;
                var initialSpeed = direction * DriverCNC.TimeScale / cnc.StartDeltaT;
                var topSpeed = direction * DriverCNC.TimeScale / 300;

                var xAcceleration = calculateBoundedAcceleration(initialSpeed.X, topSpeed.X, acceleration.X, distanceX / 2, cnc);
                var yAcceleration = calculateBoundedAcceleration(initialSpeed.Y, topSpeed.Y, acceleration.Y, distanceY / 2, cnc);

                cnc.StepperIndex = 2;
                cnc.SEND(xAcceleration);
                cnc.SEND(yAcceleration);

                var remainingX = distanceX - 2 * xAcceleration.StepCount;
                var remainingY = distanceY - 2 * yAcceleration.StepCount;
                var realSpeedX = 1.0 * DriverCNC.TimeScale / xAcceleration.EndDeltaT;
                var realSpeedY = 1.0 * DriverCNC.TimeScale / yAcceleration.EndDeltaT;
                double remainingTime;
                if (realSpeedX > realSpeedY)
                {
                    remainingTime = remainingX / realSpeedX * DriverCNC.TimeScale;
                }
                else
                {
                    remainingTime = remainingY / realSpeedY * DriverCNC.TimeScale;
                }

                SendTransition2(remainingX, remainingY, Math.Abs((long)remainingTime), cnc);

                //cnc.StepperIndex = 2;
               // cnc.SEND(xAcceleration.Invert());
               // cnc.SEND(yAcceleration.Invert());
            }
        }


        private static double calculateSteps(double startDeltaT, double endDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            var n1 = calculateN(startDeltaT, accelerationNumerator, accelerationDenominator);
            var n2 = calculateN(endDeltaT, accelerationNumerator, accelerationDenominator);

            return n2 - n1;
        }

        private static double calculateN(double startDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var n1 = (double)DriverCNC.TimeScale * DriverCNC.TimeScale * accelerationDenominator / 2 / startDeltaT / startDeltaT / DriverCNC.MaxAcceleration / accelerationNumerator;

                return n1;
            }
        }

        private static Acceleration calculateBoundedAcceleration(double initialSpeed, double endSpeed, double acceleration, int distanceLimit, DriverCNC cnc)
        {
            if (distanceLimit == 0)
                return new Acceleration(0, 0, 1, 0);
            checked
            {
                var initialDeltaT = (UInt16)Math.Round(DriverCNC.TimeScale / Math.Abs(initialSpeed));
                var endDeltaT = (UInt16)Math.Round(DriverCNC.TimeScale  / Math.Abs(endSpeed));
                var boundedAcceleration = cnc.CalculateBoundedAcceleration(initialDeltaT, endDeltaT, (Int16)(distanceLimit / 2), (int)Math.Round(acceleration), DriverCNC.MaxAcceleration);
                return boundedAcceleration;
            }
        }

        private void sendTransition3(int distanceX, int distanceY, double acceleration, DriverCNC cnc)
        {
            checked
            {
                var accelerationVect = new Vector(distanceX, distanceY);
                throw new NotImplementedException();
            }
        }

        public static void SendTransition2(int distanceX, int distanceY, long transitionTime, DriverCNC cnc)
        {
            checked
            {
                var chunkTimeLimit = 60000;

                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkCount = 1.0 * transitionTime / chunkTimeLimit;
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
                    cnc.SEND_Constant(stepCountX, stepTimeX, 0, 0);//remainderX, (UInt16)Math.Abs(stepCountXD));
                    cnc.SEND_Constant(stepCountY, stepTimeY, 0, 0);//remainderY, (UInt16)Math.Abs(stepCountYD));
                    Debug.WriteLine("{0}|{1}  {2}|{3}", stepTimeX, stepTimeY, remainderX, remainderY);
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
