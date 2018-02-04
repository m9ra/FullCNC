using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

using ControllerCNC.Primitives;
using System.Windows.Media;
using System.Windows;

namespace ControllerCNC.GUI
{
    [Serializable]
    internal abstract class PointProviderItem : WorkspaceItem
    {
        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _stepToVisualFactorC1;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _stepToVisualFactorC2;

        /// <summary>
        /// Points for cutting of the item.
        /// </summary>
        internal abstract IEnumerable<Point4Dstep> CutPoints { get; }

        /// <summary>
        /// Determine whether flexible etrance is allowed.
        /// </summary>
        internal virtual bool AllowFlexibleEntrance => true;

        /// <summary>
        /// Builds cutting plan for the item and all joined items recursively.
        /// Build assumes we are at item join point. Closed shapes has to return back to that point.
        /// </summary>
        /// <param name="workspace">Workspace where joins are defined.</param>
        /// <param name="speedPoints">Output of the build.</param>
        /// <param name="incommingJoin">Join which was used to get into the item.</param>
        internal abstract void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin);

        internal PointProviderItem(ReadableIdentifier name)
            : base(name)
        {
        }

        protected PointProviderItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <inheritdoc/>
        protected IEnumerable<Point4Dstep> translateToWorkspace(IEnumerable<Point4Dmm> points)
        {
            foreach (var point in points)
            {
                var p = point.As4Dstep();
                yield return new Point4Dstep(p.U + PositionC1, p.V + PositionC2, p.X + PositionC1, p.Y + PositionC2);
            }
        }

        /// <inheritdoc/>
        internal override void RecalculateToWorkspace(WorkspacePanel workspace, Size size)
        {
            _stepToVisualFactorC1 = size.Width / workspace.StepCountX;
            _stepToVisualFactorC2 = size.Height / workspace.StepCountY;
        }


        /// <summary>
        /// Creates figure by joining the given points.
        /// </summary>
        protected PathFigure CreatePathFigure(IEnumerable<Point2Dstep> geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var isFirst = true;
            var firstPoint = new Point(0, 0);
            foreach (var point in geometryPoints)
            {
                var visualPoint = ConvertToVisual(point);

                pathSegments.Add(new LineSegment(visualPoint, !isFirst));
                if (isFirst)
                    firstPoint = visualPoint;
                isFirst = false;
            }

            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }

        protected Point ConvertToVisual(Point2Dstep point)
        {
            var visualPoint = new Point(point.C1 - PositionC1, point.C2 - PositionC2);
            visualPoint.X = visualPoint.X * _stepToVisualFactorC1;
            visualPoint.Y = visualPoint.Y * _stepToVisualFactorC2;
            return visualPoint;
        }
    }
}
