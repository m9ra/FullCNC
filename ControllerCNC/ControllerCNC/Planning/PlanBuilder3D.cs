using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ControllerCNC.Planning
{
    public class PlanBuilder3D
    {
        /// <summary>
        /// Underlying builder.
        /// </summary>
        private readonly PlanBuilder _builder = new PlanBuilder();

        /// <summary>
        /// Where the builder starts.
        /// </summary>
        private Point3Dmm _currentPoint = null;

        /// <summary>
        /// Current zero level.
        /// </summary>
        private double _currentLevel = 0;

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
        /// Point where the builder currently is.
        /// </summary>
        public Point3Dmm CurrentPoint => _currentPoint;

        public PlanBuilder3D(double transitionLevel, double zeroLevel, Speed cuttingSpeed, Speed transitionSpeed, Acceleration planeAcceleration)
        {
            CuttingSpeed = cuttingSpeed;
            TransitionSpeed = transitionSpeed;
            PlaneAcceleration = planeAcceleration;
            _transitionLevel = transitionLevel;
            _zeroLevel = zeroLevel;
        }

        public void AddRampedLine(Point3Dmm target, Acceleration planeAcceleration, Speed planeSpeedLimit)
        {
            var diff = moveTo(target);

            _builder.AddRampedLineUVXY(diff.U, diff.V, diff.X, diff.Y, planeAcceleration, planeSpeedLimit);
        }

        public void AddConstantSpeedTransition(Point3Dmm target, Speed transitionSpeed)
        {
            var startPoint = _currentPoint;
            var diff = moveTo(target);

            _builder.AddConstantSpeedTransitionUVXY(diff.U, diff.V, transitionSpeed, diff.X, diff.Y, transitionSpeed);
        }

        public void AddRampedLine(Point2Dmm target, Acceleration planeAcceleration, Speed planeSpeedLimit)
        {
            var diff = moveTo(target);

            _builder.AddRampedLineUVXY(diff.U, diff.V, diff.X, diff.Y, planeAcceleration, planeSpeedLimit);
        }

        public void AddConstantSpeedTransition(Point2Dmm target, Speed transitionSpeed)
        {
            var startPoint = _currentPoint;
            var diff = moveTo(target);

            _builder.AddConstantSpeedTransitionUVXY(diff.U, diff.V, transitionSpeed, diff.X, diff.Y, transitionSpeed);
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
                AddRampedLine(nextTarget, PlaneAcceleration, TransitionSpeed);
            }
            else
            {
                AddConstantSpeedTransition(nextTarget, CuttingSpeed);
            }
        }

        public IEnumerable<InstructionCNC> Build()
        {
            return _builder.Build();
        }

        public void SetPosition(Point3Dmm target)
        {
            _currentPoint = target;
        }

        private Point4Dstep moveTo(Point2Dmm target)
        {
            return moveTo(new Point3Dmm(target.C1, target.C2, _currentLevel));
        }

        private Point4Dstep moveTo(Point3Dmm target)
        {
            _currentLevel = target.Z;
            var c = ToStepsRev(_currentPoint);
            var p = ToStepsRev(target);
            _currentPoint = target;

            var diffU = p.U - c.U;
            var diffV = p.V - c.V;
            var diffX = p.X - c.X;
            var diffY = p.Y - c.Y;

            return new Point4Dstep(diffU, diffV, diffX, diffY);
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

        public static Point3Dmm GetPositionFromSteps(double u, double v, double x, double y)
        {
            var m = Constants.MilimetersPerStep;
            return GetPositionFromMm(u * m, v * m, x * m, y * m);
        }

        public static int ToStep(double mm)
        {
            return (int)Math.Round(mm / Constants.MilimetersPerStep);
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
        #endregion
    }
}
