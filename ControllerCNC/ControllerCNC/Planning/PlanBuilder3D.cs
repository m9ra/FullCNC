using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ControllerCNC.Planning
{
    public class PlanBuilder3D
    {
        /// <summary>
        /// Plan stored by the builder. Instructions are not emitted directly, so it is easy to stream them.
        /// </summary>
        private readonly List<PlanPart3D> _plan = new List<PlanPart3D>();

        /// <summary>
        /// Current cutting speed for stream genertion.
        /// </summary>
        private Speed _stream_cuttingSpeed = Speed.Zero;

        /// <summary>
        /// Lock for stream generation.
        /// </summary>
        private object _L_stream = new object();

        /// <summary>
        /// Where the builder starts.
        /// </summary>
        private Point3Dmm _currentPoint = null;

        /// <summary>
        /// Current zero level.
        /// </summary>
        private double _currentLevel = 0;

        /// <summary>
        /// How much of time remains to the end of stream.
        /// </summary>
        private double _totalSeconds;

        /// <summary>
        /// Determine whether streaming should be stopped.
        /// </summary>
        private volatile bool _stop;

        /// <summary>
        /// Determine whether builder is currently streaming instructions. (not in paused mode)
        /// </summary>
        private volatile bool _isStreaming;

        /// <summary>
        /// Level where material cutting starts.
        /// </summary>
        private readonly double _zeroLevel;

        /// <summary>
        /// Level where transitions can be done.
        /// </summary>
        private readonly double _transitionLevel;

        public readonly Speed CuttingSpeed;

        public readonly Speed TransitionSpeed;

        public readonly Acceleration PlaneAcceleration;

        /// <summary>
        /// Event fired after plan streaming is complete.
        /// </summary>
        public event Action StreamingIsComplete;

        /// <summary>
        /// Point where the builder currently is.
        /// </summary>
        public Point3Dmm CurrentPoint => _currentPoint;

        public double TotalSeconds => _totalSeconds;

        public bool IsStreaming => _isStreaming;

        public double ZeroLevel => _zeroLevel;

        public PlanBuilder3D(double transitionLevel, double zeroLevel, Speed cuttingSpeed, Speed transitionSpeed, Acceleration planeAcceleration)
        {
            CuttingSpeed = cuttingSpeed;
            TransitionSpeed = transitionSpeed;
            PlaneAcceleration = planeAcceleration;
            _transitionLevel = transitionLevel;
            _zeroLevel = zeroLevel;
        }

        public void AddRampedLine(Point3Dmm target)
        {
            moveTo(target, PlaneAcceleration, TransitionSpeed);
        }

        public void AddCuttingLine(Point3Dmm target)
        {
            moveTo(target, null, CuttingSpeed);
        }

        public void AddRampedLine(Point2Dmm target)
        {
            moveTo(target, PlaneAcceleration, TransitionSpeed);
        }

        public void AddCuttingSpeedTransition(Point2Dmm target)
        {
            moveTo(target, null, CuttingSpeed);
        }

        public void AddCuttingSpeedTransition(Point2Dmm target, double zLevel)
        {
            var zTarget = new Point3Dmm(target.C1, target.C2, zLevel + _zeroLevel);
            moveTo(zTarget, null, CuttingSpeed);
        }

        public void GotoTransitionLevel()
        {
            GotoZ(_transitionLevel - _zeroLevel);
        }

        public void GotoZeroLevel()
        {
            GotoZ(0);
        }

        public void GotoZ(double zLevel)
        {
            var nextTarget = new Point3Dmm(_currentPoint.X, _currentPoint.Y, zLevel + _zeroLevel);
            if (nextTarget.Z < _currentPoint.Z)
            {
                AddRampedLine(nextTarget);
            }
            else
            {
                AddCuttingLine(nextTarget);
            }
        }

        public void StreamInstructions(DriverCNC2 driver)
        {
            var streamThread = new Thread(() =>
              {
                  _stream(driver);
              });

            streamThread.IsBackground = true;
            streamThread.Start();
        }

        private void _stream(DriverCNC2 driver)
        {
            var planStream = new PlanStream3D(_plan.ToArray());
            var preloadTime = 0.05;

            var startTime = DateTime.Now;
            var lastTimeMeasure = startTime;
            var lastTotalSecondsRefreshTime = startTime;
            var lastCuttingSpeed = Speed.Zero;
            var currentlyQueuedTime = 0.0;
            var timeQueue = new Queue<double>();
            while (!planStream.IsComplete)
            {
                while (_stop)
                {
                    _isStreaming = false;
                    Thread.Sleep(10);
                }

                _isStreaming = true;

                var currentCuttingSpeed = _stream_cuttingSpeed;
                var currentTime = DateTime.Now;

                if ((currentTime - lastTotalSecondsRefreshTime).TotalSeconds > 5.0)
                    //force recalculation every few seconds
                    lastCuttingSpeed = null;

                if (lastCuttingSpeed != currentCuttingSpeed)
                {
                    //recalculate totalSeconds
                    lastCuttingSpeed = currentCuttingSpeed;
                    var cuttingDistance = planStream.GetRemainingConstantDistance();
                    var rampTime = planStream.GetRemainingRampTime();
                    var totalElapsedTime = (currentTime - startTime).TotalSeconds;
                    _totalSeconds = totalElapsedTime + rampTime + cuttingDistance / currentCuttingSpeed.ToMetric() + currentlyQueuedTime;
                    lastTotalSecondsRefreshTime = currentTime;
                }


                var timeElapsed = (currentTime - lastTimeMeasure).TotalSeconds;
                lastTimeMeasure = currentTime;

                if (driver.IncompleteInstructionCount >= 3)
                {
                    //wait some time so we are not spinning like crazy
                    Thread.Sleep(5);
                    continue;
                }

                if (timeQueue.Count > 0)
                    currentlyQueuedTime -= timeQueue.Dequeue();

                IEnumerable<InstructionCNC> nextInstructions;
                if (planStream.IsSpeedLimitedBy(Configuration.MaxCuttingSpeed))
                {
                    var lengthLimit = preloadTime * currentCuttingSpeed.ToMetric();
                    nextInstructions = planStream.ShiftByConstantSpeed(lengthLimit, currentCuttingSpeed);
                }
                else
                {
                    nextInstructions = planStream.NextRampInstructions();
                }

                driver.SEND(nextInstructions);

                var duration = nextInstructions.Select(i => i.CalculateTotalTime());
                var timeToQueue = duration.Sum(d => (float)d) / Configuration.TimerFrequency;
                currentlyQueuedTime += timeToQueue;
                timeQueue.Enqueue(currentlyQueuedTime);
            }
            while (driver.IncompleteInstructionCount > 0)
                Thread.Sleep(10);

            StreamingIsComplete?.Invoke();
        }

        public IEnumerable<InstructionCNC> Build()
        {
            var builder = new PlanBuilder();

            foreach (var part in _plan)
            {
                var startSteps = GetPositionRev(part.StartPoint);
                var targetSteps = GetPositionRev(part.EndPoint);

                var diffU = ToStep(targetSteps.U - startSteps.U);
                var diffV = ToStep(targetSteps.V - startSteps.V);
                var diffX = ToStep(targetSteps.X - startSteps.X);
                var diffY = ToStep(targetSteps.Y - startSteps.Y);

                if (part.AccelerationRamp == null)
                {
                    builder.AddConstantSpeedTransitionUVXY(diffU, diffV, part.SpeedLimit, diffX, diffY, part.SpeedLimit);
                }
                else
                {
                    builder.AddRampedLineUVXY(diffU, diffV, diffX, diffY, part.AccelerationRamp, part.SpeedLimit);
                }
            }

            return builder.Build();
        }

        public void SetStreamingCuttingSpeed(Speed speed)
        {
            _stream_cuttingSpeed = speed;
        }

        public void SetPosition(Point3Dmm target)
        {
            _currentPoint = target;
        }

        private void moveTo(Point2Dmm target, Acceleration accelerationRamp, Speed speedLimit)
        {
            moveTo(new Point3Dmm(target.C1, target.C2, _currentLevel), accelerationRamp, speedLimit);
        }

        private void moveTo(Point3Dmm target, Acceleration accelerationRamp, Speed speedLimit)
        {
            _currentLevel = target.Z;

            if (_currentPoint.DistanceSquaredTo(target) > 0.0)
                _plan.Add(new PlanPart3D(_currentPoint, target, accelerationRamp, speedLimit));

            _currentPoint = target;
        }

        #region 4D to 3D conversions

        public static Point3Dmm GetPositionFromMm(double c1, double c2, double c3, double c4)
        {
            return new Point3Dmm(c2, c3, c4);
        }

        public static Point3Dmm GetPosition(StateInfo state)
        {
            return GetPositionFromSteps(state.U, state.V, state.X, state.Y);
        }

        public static Point4Dmm GetPositionRev(double x, double y, double z)
        {
            return new Point4Dmm(y, x, y, z);
        }

        public static Point4Dmm GetPositionRev(Point3Dmm point)
        {
            return new Point4Dmm(point.Y, point.X, point.Y, point.Z);
        }

        public static Point3Dmm GetPositionFromSteps(int u, int v, int x, int y)
        {
            var m = Configuration.MilimetersPerStep;
            return GetPositionFromMm(u * m, v * m, x * m, y * m);
        }

        public static int ToStep(double mm)
        {
            return (int)Math.Round(mm / Configuration.MilimetersPerStep);
        }

        public static Point4Dstep ToStepsRev(Point3Dmm point)
        {
            return ToStepsRev(point.X, point.Y, point.Z);
        }

        public static Point4Dstep ToStepsRev(double x, double y, double z)
        {
            var p = GetPositionRev(x, y, z);

            return new Point4Dstep(ToStep(p.U), ToStep(p.V), ToStep(p.X), ToStep(p.Y));
        }

        public static Point4Dstep GetStepDiff(Point3Dmm startPoint, Point3Dmm endPoint)
        {
            var startSteps = GetPositionRev(startPoint);
            var targetSteps = GetPositionRev(endPoint);

            var diffU = ToStep(targetSteps.U - startSteps.U);
            var diffV = ToStep(targetSteps.V - startSteps.V);
            var diffX = ToStep(targetSteps.X - startSteps.X);
            var diffY = ToStep(targetSteps.Y - startSteps.Y);

            return new Point4Dstep(diffU, diffV, diffX, diffY);
        }

        public void Stop()
        {
            if (!_isStreaming)
                return;

            _stop = true;
            while (_isStreaming)
                //spin lock, respose should be very fast
                Thread.Sleep(1);
        }

        public void Continue()
        {
            if (_isStreaming)
                return;

            _stop = false;
            while (!_isStreaming)
                //spin lock, respose should be very fast
                Thread.Sleep(1);
        }

        #endregion
    }
}
