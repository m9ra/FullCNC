using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    public class PlanStreamerContext
    {
        private readonly BlockingCollection<Action> _estimationSteps = null;

        private readonly Dictionary<ToolPathSegment, double> _edgeLimits = new Dictionary<ToolPathSegment, double>();

        private readonly HashSet<PathSpeedLimitCalculator> _openLimitCalculators = new HashSet<PathSpeedLimitCalculator>();

        private List<SegmentPlanningInfo> _workSegments = new List<SegmentPlanningInfo>();

        private ToolPathSegment _lastAddedSegment = null;

        private PlanStreamerState _currentState;

        private PlanStreamerState _timeEstimationState;

        private volatile PlanStreamerState _previewTimeEstimationState;

        private double _previewEstimationDesiredSpeed = Double.NaN;

        private double _lastEstimationDesiredSpeed = Double.NaN;

        private bool _wasEstimationLockstepDisabled = false;

        private double _totalLength = 0;

        private ulong _remainingTicksEstimation = 0;

        public bool IsComplete => _currentState != null && _currentState.IsComplete;

        public double TotalLength => _totalLength;

        public ulong RemainingTicksEstimation => _remainingTicksEstimation;

        public double CurrentSpeed
        {
            get
            {
                if (_currentState == null)
                {
                    return 0;
                }

                return _currentState.CurrentSpeed;
            }
        }


        public double CompletedLength
        {
            get
            {
                if (_currentState == null)
                {
                    return 0;
                }

                return _currentState.CompletedLength;
            }
        }

        public Point3Dstep CurrentPosition
        {
            get
            {
                if (_currentState == null)
                {
                    return null;
                }

                return _currentState.CurrentPosition;
            }
        }

        internal PlanStreamerContext(bool runRemainingTimeEstimation)
        {
            if (runRemainingTimeEstimation)
            {
                _estimationSteps = new BlockingCollection<Action>();
            }
        }

        internal void AddSegment(ToolPathSegment segment)
        {
            if (_lastAddedSegment != null)
            {
                var edgeLimit = PathSpeedLimitCalculator.CalculateEdgeLimit(_lastAddedSegment, segment);
                _edgeLimits[segment] = edgeLimit;
            }

            _lastAddedSegment = segment;
            _totalLength += segment.Length;

            var segmentInfo = new SegmentPlanningInfo(segment);
            _workSegments.Add(segmentInfo);

            foreach (var limitCalculator in _openLimitCalculators)
            {
                limitCalculator.AddLookaheadSegments(new[] { segment }, _edgeLimits);
            }

            _openLimitCalculators.Add(segmentInfo.LimitCalculator);
            foreach (var limitCalculator in _openLimitCalculators.ToArray())
            {
                if (!limitCalculator.NeedsMoreFollowingSegments)
                    _openLimitCalculators.Remove(limitCalculator);
            }
        }

        public static IEnumerable<InstructionCNC> GenerateInstructions(IEnumerable<ToolPathSegment> plan, double desiredSpeed)
        {
            var context = new PlanStreamerContext(false);
            foreach (var segment in plan)
            {
                context.AddSegment(segment);
            }

            var result = new List<InstructionCNC>();
            while (!context.IsComplete)
            {
                var instruction = context.GenerateNextInstruction(desiredSpeed);
                if (instruction != null)
                    result.Add(instruction);
            }
            return result;
        }

        internal InstructionCNC GenerateNextInstruction(double desiredSpeed, bool stopRemainingTimeLockstep = false)
        {
            if (_currentState == null)
            {
                _currentState = new PlanStreamerState(_workSegments.ToArray());
                if (_estimationSteps != null)
                {
                    _timeEstimationState = _currentState.CreateBranch();
                    var th = new Thread(_runRemainingTicksEstimation);
                    th.Priority = ThreadPriority.Lowest;
                    th.IsBackground = true;
                    th.Start();
                }

                _workSegments = null; // prevent modifications after generation started
            }

            var instruction = _currentState.GenerateNextInstruction(desiredSpeed);
            if (_timeEstimationState != null && !stopRemainingTimeLockstep)
            {
                if (!_wasEstimationLockstepDisabled && desiredSpeed == _previewEstimationDesiredSpeed)
                {
                    _estimationSteps.Add(() =>
                    {
                        moveEstimationByNextInstruction(desiredSpeed);
                    });
                }
                else
                {
                    _previewEstimationDesiredSpeed = desiredSpeed;
                    var newState = _currentState.CreateBranch();
                    _previewTimeEstimationState = newState;
                    _estimationSteps.Add(() =>
                    {
                        _lastEstimationDesiredSpeed = desiredSpeed;
                        setNewTimeEstimationState(newState);
                    });
                }
            }

            _wasEstimationLockstepDisabled = stopRemainingTimeLockstep;

            return instruction;
        }


        internal void MoveToCompletedLength(double targetLength)
        {
            targetLength = Math.Max(0, targetLength);
            targetLength = Math.Min(_totalLength, targetLength);

            _currentState.MoveToCompletedLength(targetLength);

            if (_timeEstimationState != null)
            {
                var newState = _currentState.CreateBranch();
                _previewTimeEstimationState = newState;
                _estimationSteps.Add(() => setNewTimeEstimationState(newState));
            }
        }

        private void moveEstimationByNextInstruction(double desiredSpeed)
        {
            var instruction = _timeEstimationState.GenerateNextInstruction(desiredSpeed);
            _remainingTicksEstimation -= instruction.CalculateTotalTime();

            if (_lastEstimationDesiredSpeed != desiredSpeed)
            {
                _lastEstimationDesiredSpeed = desiredSpeed;
                recalculateRemainingTime();
            }
        }


        private void setNewTimeEstimationState(PlanStreamerState state)
        {
            _timeEstimationState = state;
            recalculateRemainingTime();
        }

        private void recalculateRemainingTime()
        {
            // we need to recalculate the state from current point
            var stateCopy = _timeEstimationState.CreateBranch();
            var remainingTicks = 0UL;
            while (!stateCopy.IsComplete)
            {
                if (_previewTimeEstimationState != _timeEstimationState)
                    // early stopping - the recalculation will run soon again
                    return;

                var instruction = stateCopy.GenerateNextInstruction(_lastEstimationDesiredSpeed);
                remainingTicks += instruction.CalculateTotalTime();
            }
            _remainingTicksEstimation = remainingTicks;
        }

        private void _runRemainingTicksEstimation()
        {
            while (true)
            {
                var action = _estimationSteps.Take();
                action();
            }
        }
    }

    class SegmentPlanningInfo
    {
        public readonly ToolPathSegment Segment;

        public readonly PathSpeedLimitCalculator LimitCalculator;

        internal SegmentPlanningInfo(ToolPathSegment segment)
        {
            Segment = segment;
            LimitCalculator = new PathSpeedLimitCalculator(segment);
        }
    }
}
