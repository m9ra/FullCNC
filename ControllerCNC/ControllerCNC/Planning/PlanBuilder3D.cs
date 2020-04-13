using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

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
        private double _stream_cuttingSpeed = 0;

        /// <summary>
        /// Lock for stream generation.
        /// </summary>
        private object _L_speed = new object();

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
        private double _remainingSeconds;

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

        private PlanStreamerContext _context = null;

        public readonly Speed CuttingSpeed;

        public readonly Speed TransitionSpeed;

        public readonly Acceleration PlaneAcceleration;

        public IEnumerable<PlanPart3D> PlanParts => _plan;

        /// <summary>
        /// Event fired after plan streaming is complete.
        /// </summary>
        public event Action StreamingIsComplete;

        /// <summary>
        /// Point where the builder currently is.
        /// </summary>
        public Point3Dmm CurrentPoint => _currentPoint;

        public double RemainingSeconds => _remainingSeconds;

        public bool IsStreaming => _isStreaming;

        public bool IsChangingPosition { get; private set; }

        public double ZeroLevel => _zeroLevel;

        public double Progress
        {
            get
            {
                if (_context == null)
                {
                    return 0;
                }

                return _context.CompletedLength / _context.TotalLength;
            }
        }

        public Point3Dstep CurrentStreamPosition
        {
            get
            {
                if (_context == null)
                    return null;

                return _context.CurrentPosition;
            }
        }

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
            PlanStreamerContext intermContext = null;
            _context = new PlanStreamerContext();
            foreach (var part in PlanParts)
            {
                var s = part.StartPoint;
                var e = part.EndPoint;

                var segment = new ToolPathSegment(new Point3D(s.X, s.Y, s.Z), new Point3D(e.X, e.Y, e.Z), MotionMode.IsLinear);
                _context.AddSegment(segment);
            }

            var zeroCompatibleSpeed = Configuration.ReverseSafeSpeed.ToMetric();
            var instructionBuffer = new Queue<InstructionCNC>();
            var maxPlannedInstructionCount = 5;

            var currentContext = _context;

            while (!currentContext.IsComplete || intermContext != null)
            {
                if (intermContext != null && intermContext.IsComplete)
                {
                    intermContext = null;
                    currentContext = _context;
                    continue;
                }

                while (_stop && _context.CurrentSpeed <= zeroCompatibleSpeed)
                {
                    _isStreaming = false;
                    Thread.Sleep(10);
                }
                _isStreaming = true;

                if (IsChangingPosition)
                {
                    // create intermediate context which will transfer machine to the new position
                    var np = CurrentStreamPosition.As3Dmm();
                    var cp = driver.CurrentState.As3Dstep().As3Dmm();
                    var cpTransition = new Point3Dmm(cp.X, cp.Y, _transitionLevel);
                    var npTransition = new Point3Dmm(np.X, np.Y, _transitionLevel);

                    intermContext = new PlanStreamerContext();
                    intermContext.AddSegment(new ToolPathSegment(cp.As3D(), cpTransition.As3D(), MotionMode.IsLinear));
                    intermContext.AddSegment(new ToolPathSegment(cpTransition.As3D(), npTransition.As3D(), MotionMode.IsLinear));
                    intermContext.AddSegment(new ToolPathSegment(npTransition.As3D(), np.As3D(), MotionMode.IsLinear));

                    currentContext = intermContext;
                    IsChangingPosition = false;
                }

                if (driver.IncompleteInstructionCount >= maxPlannedInstructionCount)
                {
                    //wait some time so we are not spinning like crazy
                    Thread.Sleep(1);
                    continue;
                }

                do
                {
                    double speed;
                    lock (_L_speed)
                        speed = _stream_cuttingSpeed;

                    if (_stop)
                        speed = zeroCompatibleSpeed;

                    var instruction = currentContext.GenerateNextInstruction(speed);
                    instructionBuffer.Enqueue(instruction);
                } while (driver.IncompleteInstructionCount == 0 && !currentContext.IsComplete && instructionBuffer.Count < maxPlannedInstructionCount);

                while (instructionBuffer.Count > 0)
                {
                    var instruction = instructionBuffer.Dequeue();
                    if (!driver.SEND(instruction))
                        throw new InvalidOperationException("Instruction was not accepted. (Probably over bounds?). Progress: " + Progress * 100);
                }
            }

            while (driver.IncompleteInstructionCount > 0)
                //wait until all instructions are completed
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

        public void SetStreamingCuttingSpeed(double speed)
        {
            lock (_L_speed)
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

        public static Axes Combine(StepInstrution x, StepInstrution y, StepInstrution z)
        {
            return Axes.UVXY(y, x, y, z);
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
                //spin lock, response should be very fast
                Thread.Sleep(1);
        }

        public void Continue()
        {
            if (_isStreaming)
                return;

            _stop = false;
            while (!_isStreaming)
                //spin lock, response should be very fast
                Thread.Sleep(1);
        }

        public void SetProgress(double value)
        {
            if (_isStreaming)
                throw new InvalidOperationException("Can't change progress while streaming");

            IsChangingPosition = true;
            _context.MoveToCompletedLength(_context.TotalLength * value);
        }

        #endregion
    }
}
