using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    public class PlanStreamerContext
    {
        private readonly List<SegmentPlanningInfo> _workSegments = new List<SegmentPlanningInfo>();

        private readonly Dictionary<ToolPathSegment, double> _edgeLimits = new Dictionary<ToolPathSegment, double>();

        private readonly HashSet<PathSpeedLimitCalculator> _openLimitCalculators = new HashSet<PathSpeedLimitCalculator>();

        private ToolPathSegment _lastAddedSegment = null;

        private ToolPathSegmentSlicer _currentSlicer = null;

        private SegmentPlanningInfo _currentSegmentInfo = null;

        private int _nextWorkSegmentIndex = 0;

        private double _currentSpeed = 0;

        private double _completedLength = 0;

        private double _totalLength = 0;

        public bool IsComplete => _nextWorkSegmentIndex >= _workSegments.Count && (_currentSlicer == null || _currentSlicer.IsComplete);

        public double CurrentSpeed => _currentSpeed;

        public double CompletedLength
        {
            get
            {
                if (_currentSlicer == null)
                    return _completedLength;

                return _currentSlicer.CompletedLength + _completedLength;
            }
        }

        public double TotalLength => _totalLength;

        public Point3Dstep CurrentPosition
        {
            get
            {
                if (_currentSlicer == null)
                {
                    return null;
                }

                return _currentSlicer.CurrentPosition;
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
            var context = new PlanStreamerContext();
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

        internal InstructionCNC GenerateNextInstruction(double desiredSpeed)
        {
            if (_currentSlicer == null || _currentSlicer.IsComplete)
            {
                if (_currentSlicer != null)
                {
                    _completedLength += _currentSlicer.CompletedLength;
                }

                _currentSegmentInfo = _workSegments[_nextWorkSegmentIndex];
                _currentSlicer = new ToolPathSegmentSlicer(_currentSegmentInfo.Segment);
                _nextWorkSegmentIndex += 1;
            }

            var limitCalculator = _currentSegmentInfo.LimitCalculator;
            var currentSegment = _currentSegmentInfo.Segment;
            var smoothingLookahead = 1.0 / currentSegment.Length;
            var speedLimit = limitCalculator.GetLimit(_currentSlicer.SliceProgress);

            double speedLimitLookahead;
            if (_currentSpeed < speedLimit)
            {
                // lookahead is useful to prevent short term accelerations - call it only when acceleration is possible
                speedLimitLookahead = limitCalculator.GetLimit(Math.Min(1.0, _currentSlicer.SliceProgress + smoothingLookahead));
            }
            else
            {
                speedLimitLookahead = speedLimit;
            }

            var targetSpeed = Math.Min(desiredSpeed, speedLimit);
            if (_currentSpeed < speedLimitLookahead)
                //allow acceleration only when limit is far enough
                _currentSpeed = PathSpeedLimitCalculator.TransitionSpeedTo(_currentSpeed, targetSpeed);

            var newSpeed = Math.Min(_currentSpeed, speedLimit); //speed limits are valid (acceleration limits accounted)
            _currentSpeed = newSpeed;

            return _currentSlicer.Slice(_currentSpeed, PathSpeedLimitCalculator.TimeGrain);
        }

        internal void MoveToCompletedLength(double targetLength)
        {
            targetLength = Math.Max(0, targetLength);
            targetLength = Math.Min(_totalLength, targetLength);

            --_nextWorkSegmentIndex;

            if (_nextWorkSegmentIndex < 1)
            {
                _completedLength = 0;
                _nextWorkSegmentIndex = 0;
            }


            while (_completedLength > targetLength)
            {
                --_nextWorkSegmentIndex;
                _currentSegmentInfo = _workSegments[_nextWorkSegmentIndex];
                _completedLength -= _currentSegmentInfo.Segment.Length;
            }

            while (_completedLength < targetLength && _nextWorkSegmentIndex < _workSegments.Count)
            {
                _currentSegmentInfo = _workSegments[_nextWorkSegmentIndex];
                if (_completedLength + _currentSegmentInfo.Segment.Length >= targetLength)
                {
                    // we found the correct segment
                    break;
                }

                ++_nextWorkSegmentIndex;
                _completedLength += _currentSegmentInfo.Segment.Length;
            }

            _currentSegmentInfo = _workSegments[_nextWorkSegmentIndex];
            _currentSlicer = new ToolPathSegmentSlicer(_currentSegmentInfo.Segment);
            ++_nextWorkSegmentIndex;
            while (_currentSlicer.SliceProgress * _currentSlicer.Segment.Length + _completedLength < targetLength)
            {
                _currentSlicer.Slice(_currentSpeed, PathSpeedLimitCalculator.TimeGrain);

                if (_currentSlicer.IsComplete)
                    break;
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
