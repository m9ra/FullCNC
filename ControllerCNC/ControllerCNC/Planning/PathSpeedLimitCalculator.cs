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
    internal class PathSpeedLimitCalculator
    {
        internal readonly ToolPathSegment ActiveSegment;

        internal bool NeedsMoreFollowingSegments => _lookaheadDistance < _maxLookaheadDistance;

        internal int FollowingSegmentCount { get; private set; }

        public static readonly double TimeGrain = 0.02;

        private readonly List<ToolPathSegment> _followingSegments = new List<ToolPathSegment>();

        private double _lookaheadDistance = 0.0;

        private double _cornerLimit;

        private double _optimisticCornerLimit;

        private static readonly int[] _accelerationRanges;

        private static readonly double _maxLookaheadDistance;

        static PathSpeedLimitCalculator()
        {
            var acceleration = AccelerationBuilder.FromTo(Configuration.ReverseSafeSpeed, Configuration.MaxPlaneSpeed, Configuration.MaxPlaneAcceleration, 10000);
            var instruction = acceleration.ToInstruction();
            _accelerationRanges = instruction.GetStepTimings();


            _maxLookaheadDistance = accelerateFrom(Configuration.ReverseSafeSpeed.ToMetric(), 10000, out _maxLookaheadDistance);
        }

        internal PathSpeedLimitCalculator(ToolPathSegment activeSegment)
        {
            ActiveSegment = activeSegment;

            _lookaheadDistance = 0.0;
            _cornerLimit = Configuration.ReverseSafeSpeed.ToMetric();
            _optimisticCornerLimit = Configuration.MaxPlaneSpeed.ToMetric();
        }

        internal static double TransitionSpeedTo(double currentSpeed, double targetSpeed)
        {
            if (currentSpeed == targetSpeed)
                return currentSpeed;

            var direction = Math.Sign(targetSpeed - currentSpeed);

            var i1 = GetAccelerationIndex(currentSpeed, direction >= 0);
            var i2 = GetAccelerationIndex(targetSpeed, direction >= 0);
            if (Math.Abs(i1 - i2) <= 1)
            {
                return targetSpeed;
            }

            var nextDeltaT = _accelerationRanges[i1 + direction];
            return Speed.FromDeltaT(nextDeltaT).ToMetric();
        }

        internal static int GetAccelerationIndex(double speed, bool isAcceleration)
        {
            if (speed == 0)
                speed = Configuration.ReverseSafeSpeed.ToMetric();

            var ticks = Speed.FromMilimetersPerSecond(speed).ToDeltaT();

            if (isAcceleration)
            {
                for (var i = 0; i < _accelerationRanges.Length; ++i)
                {
                    if (ticks >= _accelerationRanges[i])
                    {
                        return i;
                    }
                }

                return _accelerationRanges.Length;

            }
            else
            {
                for (var i = _accelerationRanges.Length - 1; i >= 0; --i)
                {
                    if (ticks <= _accelerationRanges[i])
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        internal object GetLimit(object position)
        {
            throw new NotImplementedException();
        }

        internal static double GetAxisLimit(double r1, double r2)
        {
            var ar1 = Math.Abs(r1);
            var ar2 = Math.Abs(r2);
            var maxScaleFactor = Math.Max(ar1, ar2);
            var minScaleFactor = Math.Min(ar1, ar2);

            if (r1 == r2)
                return Configuration.MaxPlaneSpeed.ToMetric();

            var limit = Speed.FromDeltaT(Configuration.StartDeltaT).ToMetric() / maxScaleFactor;

            if (r1 * r2 < 0 || r1 * r2 == 0)
                // direction change
                return limit;

            for (var i = 0; i < _accelerationRanges.Length - 1; ++i)
            {
                //TODO binary search
                var ac1 = Speed.FromDeltaT(_accelerationRanges[i]).ToMetric();
                var ac2 = Speed.FromDeltaT(_accelerationRanges[i + 1]).ToMetric();

                var sp1 = ac1;
                var sp2 = ac1 / minScaleFactor;

                if (sp2 >= ac2)
                    break;

                limit = ac1 / maxScaleFactor;
            }

            return limit;
        }

        internal double GetLimit(double positionPercentage)
        {
            var activeSegmentRemainingLength = (ActiveSegment.End - ActiveSegment.Start).Length * (1.0 - positionPercentage);

            return accelerateFrom(_cornerLimit, activeSegmentRemainingLength, out _);
        }

        internal void AddLookaheadSegments(IEnumerable<ToolPathSegment> workSegments, Dictionary<ToolPathSegment, double> cornerLimits)
        {
            foreach (var segment in workSegments)
            {
                if (!NeedsMoreFollowingSegments)
                    break;

                var segmentCornerLimit = cornerLimits[segment];
                var reachableSpeed = accelerateFrom(segmentCornerLimit, _lookaheadDistance, out _);

                _optimisticCornerLimit = Math.Min(_optimisticCornerLimit, reachableSpeed);

                _lookaheadDistance += segment.Length;

                //limit is min from each lookahead corner and final stop
                var safeSpeed = accelerateFrom(Configuration.ReverseSafeSpeed.ToMetric(), _lookaheadDistance, out _);
                _cornerLimit = Math.Min(safeSpeed, _optimisticCornerLimit);
            }
        }

        private static double accelerateFrom(double startingSpeed, double distance, out double distanceBeforeMax)
        {
            var maxSpeed = Configuration.MaxPlaneSpeed.ToMetric();

            var actualSpeed = startingSpeed;
            var actualDistance = 0.0;
            do
            {
                var grainDistance = TimeGrain * actualSpeed;
                actualSpeed = TransitionSpeedTo(actualSpeed, double.PositiveInfinity);

                actualDistance += grainDistance;
                distanceBeforeMax = actualDistance;

                if (actualSpeed >= Configuration.MaxPlaneSpeed.ToMetric())
                    break;

            } while (actualDistance < distance);
            return actualSpeed;
        }
    }
}
