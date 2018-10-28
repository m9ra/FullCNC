using ControllerCNC.Machine;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    class PlanStreamerContext
    {
        private readonly Queue<SegmentPlanningInfo> _workSegments = new Queue<SegmentPlanningInfo>();

        private readonly Dictionary<ToolPathSegment, double> _edgeLimits = new Dictionary<ToolPathSegment, double>();

        private readonly HashSet<PathSpeedLimitCalculator> _openLimitCalculators = new HashSet<PathSpeedLimitCalculator>();

        private ToolPathSegment _lastAddedSegment = null;

        private ToolPathSegmentSlicer _currentSlicer = null;

        private SegmentPlanningInfo _currentSegmentInfo = null;

        private double _currentSpeed = 0;

        public bool IsComplete => _workSegments.Count == 0 && (_currentSlicer == null || _currentSlicer.IsComplete);

        public double CurrentSpeed => _currentSpeed;

        internal void AddSegment(ToolPathSegment segment)
        {
            if (_lastAddedSegment != null)
            {
                var edgeLimit = PathSpeedLimitCalculator.CalculateEdgeLimit(_lastAddedSegment, segment);
                _edgeLimits[segment] = edgeLimit;
            }

            _lastAddedSegment = segment;

            var segmentInfo = new SegmentPlanningInfo(segment);
            _workSegments.Enqueue(segmentInfo);

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

        internal InstructionCNC GenerateNextInstruction(double desiredSpeed)
        {
            if (_currentSlicer == null || _currentSlicer.IsComplete)
            {
                _currentSegmentInfo = _workSegments.Dequeue();
                if (_currentSegmentInfo == null)
                    return null;

                _currentSlicer = new ToolPathSegmentSlicer(_currentSegmentInfo.Segment);
            }

            var limitCalculator = _currentSegmentInfo.LimitCalculator;
            var currentSegment = _currentSegmentInfo.Segment;
            var smoothingLookahead = 1.0 / currentSegment.Length;
            var speedLimit = limitCalculator.GetLimit(_currentSlicer.Position);
            var speedLimitLookahead = limitCalculator.GetLimit(Math.Min(1.0, _currentSlicer.Position + smoothingLookahead));

            var targetSpeed = Math.Min(desiredSpeed, speedLimit);
            if (_currentSpeed < speedLimitLookahead)
                //allow acceleration only when limit is far enough
                _currentSpeed = PathSpeedLimitCalculator.TransitionSpeedTo(_currentSpeed, targetSpeed);

            var newSpeed = Math.Min(_currentSpeed, speedLimit); //speed limits are valid (acceleration limits accounted)
            _currentSpeed = newSpeed;

            return _currentSlicer.Slice(_currentSpeed, PathSpeedLimitCalculator.TimeGrain);
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
