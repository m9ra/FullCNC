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
    class PlanStreamerState
    {
        private readonly SegmentPlanningInfo[] _workSegments;

        private ToolPathSegmentSlicer _currentSlicer = null;

        private SegmentPlanningInfo _currentSegmentInfo = null;

        private double _currentSpeed = 0;

        private int _nextWorkSegmentIndex = 0;

        private double _completedLength = 0;

        internal bool IsComplete => _nextWorkSegmentIndex >= _workSegments.Length && (_currentSlicer == null || _currentSlicer.IsComplete);

        internal double CurrentSpeed => _currentSpeed;

        internal double CompletedLength
        {
            get
            {
                if (_currentSlicer == null)
                    return _completedLength;

                return _currentSlicer.CompletedLength + _completedLength;
            }
        }

        internal Point3Dstep CurrentPosition
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

        internal PlanStreamerState(SegmentPlanningInfo[] workSegments)
        {
            _workSegments = workSegments;
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
            var smoothingLookahead = 5.0 / currentSegment.Length;
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

            while (_completedLength < targetLength && _nextWorkSegmentIndex < _workSegments.Length)
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

        internal PlanStreamerState CreateBranch()
        {
            var branchState = (PlanStreamerState)MemberwiseClone();
            if (branchState._currentSlicer != null)
            {
                branchState._currentSlicer = branchState._currentSlicer.DeepCopy();
            }

            return branchState;
        }
    }
}
