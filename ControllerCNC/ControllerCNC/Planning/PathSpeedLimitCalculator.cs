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
    public class PathSpeedLimitCalculator
    {
        internal readonly ToolPathSegment ActiveSegment;

        internal bool NeedsMoreFollowingSegments => _lookaheadDistance < _maxLookaheadDistance;

        internal int FollowingSegmentCount { get; private set; }

        public static readonly double TimeGrain = 0.02;

        public static int[] AccelerationRanges => _accelerationRanges.ToArray();

        private readonly List<ToolPathSegment> _followingSegments = new List<ToolPathSegment>();

        private double _lookaheadDistance = 0.0;

        private double _cornerLimit;

        private double _optimisticCornerLimit;

        private static readonly int[] _accelerationRanges;

        private static readonly double[] _accelerationRangesMetric;

        private static readonly double[] _accelerationDistances;

        private static readonly double[] _accelerationSpeeds;

        private static readonly double _maxLookaheadDistance;

        static PathSpeedLimitCalculator()
        {
            var ranges = new List<int>();
            var rangesMetric = new List<double>();
            var v0 = Configuration.ReverseSafeSpeed.ToMetric();
            var v = v0;
            var vmax = Configuration.MaxPlaneSpeed.ToMetric();
            var currentTime = 0;
            while (v < vmax)
            {
                rangesMetric.Add(v);
                ranges.Add(Speed.FromMilimetersPerSecond(v).ToDeltaT());
                //v=v0 + 1/2 a*t
                currentTime += ranges.Last();

                var t = 1.0 * currentTime / Configuration.TimerFrequency;
                v = v0 + 0.5 * t * Configuration.MaxPartialAcceleration * Configuration.MilimetersPerStep;
            }

            _accelerationRanges = ranges.ToArray();
            _accelerationRangesMetric = rangesMetric.ToArray();

            calculateAccelerationLimits(out _accelerationDistances, out _accelerationSpeeds);
            _maxLookaheadDistance = accelerateFrom(Configuration.ReverseSafeSpeed.ToMetric(), 10000, out _maxLookaheadDistance);
        }

        public PathSpeedLimitCalculator(ToolPathSegment activeSegment)
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

            currentSpeed = Math.Max(currentSpeed, Configuration.ReverseSafeSpeedMetric);

            var direction = Math.Sign(targetSpeed - currentSpeed);
            var currentDeltaT = Speed.FromMilimetersPerSecond(currentSpeed).ToDeltaT();
            var t = 1.0 * currentDeltaT / Configuration.TimerFrequency;
            var newSpeed = currentSpeed + 0.5 * direction * t * Configuration.MaxPartialAcceleration * Configuration.MilimetersPerStep;

            if (targetSpeed > currentSpeed)
            {
                if (newSpeed > targetSpeed)
                    newSpeed = targetSpeed;
            }
            else
            {
                if (newSpeed < targetSpeed)
                    newSpeed = targetSpeed;
            }

            return newSpeed;
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

            var speedupFactor = maxScaleFactor / minScaleFactor;
            for (var i = 0; i < _accelerationRanges.Length - 1; ++i)
            {
                //TODO binary search
                var ac1 = _accelerationRangesMetric[i];
                var ac2 = _accelerationRangesMetric[i + 1];

                var sp1 = ac1;
                var sp2 = ac1 * speedupFactor;
                if (sp2 >= ac2)
                    break;

                limit = ac2;
            }

            return limit;
        }

        public double GetLimit(double positionPercentage)
        {
            var activeSegmentRemainingLength = (ActiveSegment.End - ActiveSegment.Start).Length * (1.0 - positionPercentage);
            return accelerateFrom(_cornerLimit, activeSegmentRemainingLength, out _);
        }

        public void AddLookaheadSegments(IEnumerable<ToolPathSegment> workSegments, Dictionary<ToolPathSegment, double> cornerLimits)
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

        public static double CalculateEdgeLimit(ToolPathSegment segment1, ToolPathSegment segment2)
        {
            if (segment1 == null)
                return Configuration.ReverseSafeSpeed.ToMetric();

            CalculateRatios(segment1, out var rX1, out var rY1, out var rZ1);
            CalculateRatios(segment2, out var rX2, out var rY2, out var rZ2);

            var limitX = GetAxisLimit(rX1, rX2);
            var limitY = GetAxisLimit(rY1, rY2);
            var limitZ = GetAxisLimit(rZ1, rZ2);

            return Math.Min(Math.Min(limitX, limitY), limitZ);
        }

        public static void CalculateRatios(ToolPathSegment segment, out double rX, out double rY, out double rZ)
        {
            var v = segment.End - segment.Start;
            v.Normalize();

            rX = v.X;
            rY = v.Y;
            rZ = v.Z;
        }

        private static void calculateAccelerationLimits(out double[] distances, out double[] speeds)
        {
            var maxSpeed = Configuration.MaxPlaneSpeedMetric;
            var actualSpeed = Configuration.ReverseSafeSpeedMetric;
            var actualDistance = 0.0;

            var distancesL = new List<double>();
            var speedsL = new List<double>();

            distancesL.Add(actualDistance);
            speedsL.Add(actualSpeed);
            do
            {
                var grainDistance = TimeGrain * actualSpeed;
                actualSpeed = TransitionSpeedTo(actualSpeed, double.PositiveInfinity);
                actualDistance += grainDistance;

                distancesL.Add(actualDistance);
                speedsL.Add(actualSpeed);

            } while (actualSpeed < maxSpeed);
            
            distances = distancesL.ToArray();
            speeds = speedsL.ToArray();
        }

        private static double accelerateFrom(double startingSpeed, double distance, out double distanceBeforeMax)
        {
            var lengthOffset = 0.0;
            for (var i = 0; i < _accelerationSpeeds.Length; ++i)
            {
                var speed = _accelerationSpeeds[i];
                if (speed > startingSpeed)
                    break;

                lengthOffset = _accelerationDistances[i];
            }

            double reachedSpeed;
            var offsetedDistance = distance + lengthOffset;
            var j = _accelerationSpeeds.Length;
            do
            {
                j -= 1;
                reachedSpeed = _accelerationSpeeds[j];
                distanceBeforeMax = _accelerationDistances[j];
            } while (offsetedDistance < distanceBeforeMax);

            return reachedSpeed;
        }
    }
}
