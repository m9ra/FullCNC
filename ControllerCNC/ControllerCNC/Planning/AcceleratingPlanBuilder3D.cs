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

        private readonly int _maxPlannedInstructionCount = 5;

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

            var edgeLimit = PathSpeedLimitCalculator.CalculateEdgeLimit(previousSegment, segment);

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
            var instructionBuffer = new Queue<InstructionCNC>();
            while (true)
            {
                ToolPathSegment currentSegment = null;
                PathSpeedLimitCalculator limitCalculator;
                lock (_L_workSegments)
                {
                    while (_workSegments.Count == 0)
                        Monitor.Wait(_L_workSegments);

                    currentSegment = _workSegments.Dequeue();
                    limitCalculator = new PathSpeedLimitCalculator(currentSegment);
                    limitCalculator.AddLookaheadSegments(_workSegments, _edgeLimits);
                }

                var smoothingLookahead = 1.0 / currentSegment.Length;
                var slicer = new ToolPathSegmentSlicer(currentSegment);
                while (!slicer.IsComplete)
                {
                    var desiredSpeed = getDesiredSpeed();
                    var speedLimit = limitCalculator.GetLimit(slicer.Position);
                    var speedLimitLookahead = limitCalculator.GetLimit(Math.Min(1.0, slicer.Position + smoothingLookahead));

                    var targetSpeed = Math.Min(desiredSpeed, speedLimit);
                    if (currentSpeed < speedLimitLookahead)
                        //allow acceleration only when limit is far enough
                        currentSpeed = PathSpeedLimitCalculator.TransitionSpeedTo(currentSpeed, targetSpeed);

                    var newSpeed = Math.Min(currentSpeed, speedLimit); //speed limits are valid (acceleration limits accounted)
                    if (newSpeed == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    currentSpeed = newSpeed;

                    var nextInstruction = slicer.Slice(currentSpeed, PathSpeedLimitCalculator.TimeGrain);
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
                                    limitCalculator.AddLookaheadSegments(_workSegments.Skip(limitCalculator.FollowingSegmentCount), _edgeLimits);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                        cn += 1;
                    }

                    instructionBuffer.Enqueue(nextInstruction);
                    if (_cnc.IncompleteInstructionCount == 0 && instructionBuffer.Count < _maxPlannedInstructionCount)
                    {
                        //buffer instructions so the machine has full buffer right away
                        if (!slicer.IsComplete)
                            continue;
                    }

                    while (instructionBuffer.Count > 0)
                    {
                        var bufferedInstruction = instructionBuffer.Dequeue();
                        if (!_cnc.SEND(bufferedInstruction))
                            throw new NotSupportedException("Invalid instruction");
                    }
                }
            }
        }

        private double getDesiredSpeed()
        {
            lock (_L_desiredSpeed)
                return _desiredSpeed;
        }
    }
}
