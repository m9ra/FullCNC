using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GeometryCNC.Primitives;
using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    public class AcceleratingPlanBuilder3D
    {
        private readonly DriverCNC2 _cnc;

        private readonly object _L_workSegments = new object();

        private readonly object _L_desiredSpeed = new object();

        private readonly Queue<ToolPathSegment> _workSegments = new Queue<ToolPathSegment>();

        private readonly Dictionary<ToolPathSegment, double> _edgeLimits = new Dictionary<ToolPathSegment, double>();

        private readonly double _timeGrain = 0.01;

        private readonly int _maxPlannedInstructionCount = 0;

        // is used to calculate corner speed limits
        private ToolPathSegment _lastAddedSegment;

        private double _desiredSpeed = 0.0;

        public AcceleratingPlanBuilder3D(DriverCNC2 cnc)
        {
            _cnc = cnc;

            var streamer = new Thread(_streamer);
            streamer.IsBackground = true;
            streamer.Priority = ThreadPriority.Highest;
            streamer.Start();
        }

        public void SetDesiredSpeed(double speed)
        {
            lock (_L_desiredSpeed)
                _desiredSpeed = speed;
        }

        public void Add(ToolPathSegment segment)
        {
            ToolPathSegment previousSegment;

            lock (_L_workSegments)
            {
                previousSegment = _lastAddedSegment;
            }

            var edgeLimit = calculateEdgeLimit(previousSegment, segment);

            lock (_L_workSegments)
            {
                if (previousSegment != _lastAddedSegment)
                    throw new InvalidOperationException("Race condition detected. Segments cannot be added asynchronously.");

                _workSegments.Enqueue(segment);
                _edgeLimits[segment] = edgeLimit;

                _lastAddedSegment = segment;
            }
        }

        private void _streamer()
        {
            var currentSpeed = 0.0;
            while (true)
            {
                ToolPathSegment currentSegment = null;
                PathSpeedLimitCalculator limitCalculator;
                lock (_L_workSegments)
                {
                    while (_workSegments.Count == 0)
                    {
                        Monitor.Wait(_L_workSegments);
                    }

                    currentSegment = _workSegments.Dequeue();
                    limitCalculator = new PathSpeedLimitCalculator(currentSegment, _timeGrain);
                    limitCalculator.AddFollowingSegments(_workSegments, _edgeLimits);
                }

                var slicer = new ToolPathSegmentSlicer(currentSegment);
                while (!slicer.IsComplete)
                {
                    var desiredSpeed = getDesiredSpeed();
                    var speedLimit = limitCalculator.GetLimit(slicer.Position);
                    var targetSpeed = Math.Min(desiredSpeed, speedLimit);

                    currentSpeed = PathSpeedLimitCalculator.TransitionSpeedTo(currentSpeed, targetSpeed, _timeGrain);
                    var newSpeed = Math.Min(currentSpeed, speedLimit); //speed limits are valid (acceleration limits accounted)

                    if (newSpeed == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    currentSpeed = newSpeed;

                    var nextInstruction = slicer.Slice(currentSpeed, _timeGrain);

                    var cn = 0;
                    while (_cnc.IncompleteInstructionCount > _maxPlannedInstructionCount)
                    {
                        // spin wait
                        if (cn % 10 == 0)
                        {
                            if (limitCalculator.NeedsMoreFollowingSegments)
                            {
                                lock (_L_workSegments)
                                {
                                    limitCalculator.AddFollowingSegments(_workSegments.Skip(limitCalculator.FollowingSegmentCount), _edgeLimits);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                        cn += 1;
                    }
                    if (!_cnc.SEND(nextInstruction))
                        throw new NotSupportedException("Invalid instruction");
                }
            }
        }

        private double getDesiredSpeed()
        {
            lock (_L_desiredSpeed)
                return _desiredSpeed;
        }

        private double calculateEdgeLimit(ToolPathSegment segment1, ToolPathSegment segment2)
        {
            if (segment1 == null)
                return Configuration.ReverseSafeSpeed.ToMetric();

            calculateRatios(segment1, out var rX1, out var rY1, out var rZ1);
            calculateRatios(segment2, out var rX2, out var rY2, out var rZ2);

            var limitX = PathSpeedLimitCalculator.GetAxisLimit(rX1, rX2);
            var limitY = PathSpeedLimitCalculator.GetAxisLimit(rY1, rY2);
            var limitZ = PathSpeedLimitCalculator.GetAxisLimit(rZ1, rZ2);

            return Math.Min(Math.Min(limitX, limitY), limitZ);
        }

        private void calculateRatios(ToolPathSegment segment, out double rX, out double rY, out double rZ)
        {
            var length = (segment.End - segment.Start).Length;
            var s = segment.Start;
            var e = segment.End;

            var lX = s.X - e.X;
            var lY = s.Y - e.Y;
            var lZ = s.Z - e.Z;
            rX = lX / length;
            rY = lY / length;
            rZ = lZ / length;
        }
    }
}
