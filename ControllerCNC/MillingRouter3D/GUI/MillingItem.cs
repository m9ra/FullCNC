using ControllerCNC.Planning;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MillingRouter3D.GUI
{
    abstract class MillingItem : MillingWorkspaceItem
    {
        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _mmToVisualFactorC1;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _mmToVisualFactorC2;

        internal virtual Point2Dmm EntryPoint => getEntryPoint();

        internal abstract void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace);

        protected abstract Point2Dmm getEntryPoint();

        internal MillingItem(ReadableIdentifier name)
                 : base(name)
        {
        }

        protected MillingItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }


        /// <summary>
        /// Creates figure by joining the given points.
        /// </summary>
        protected PathGeometry CreatePathFigure(IEnumerable<Point2Dmm[]> geometryPointClusters)
        {
            var figures = new List<PathFigure>();
            foreach (var geometryPoints in geometryPointClusters)
            {
                var pathSegments = new PathSegmentCollection();
                var firstPoint = new Point(0, 0);
                var isFirst = true;
                foreach (var point in geometryPoints)
                {
                    var visualPoint = ConvertToVisual(point);

                    pathSegments.Add(new LineSegment(visualPoint, !isFirst));
                    if (isFirst)
                        firstPoint = visualPoint;
                    isFirst = false;
                }
                var figure = new PathFigure(firstPoint, pathSegments, false);
                figures.Add(figure);
            }
            var geometry = new PathGeometry(figures, FillRule.EvenOdd, Transform.Identity);
            return geometry;
        }

        /// <inheritdoc/>
        internal override void RecalculateToWorkspace(MillingWorkspacePanel workspace, Size size)
        {
            _mmToVisualFactorC1 = size.Width / workspace.RangeX;
            _mmToVisualFactorC2 = size.Height / workspace.RangeY;
        }

        protected Point ConvertToVisual(Point2Dmm point)
        {
            var visualPoint = new Point(point.C1 - PositionX, point.C2 - PositionY);
            visualPoint.X = visualPoint.X * _mmToVisualFactorC1;
            visualPoint.Y = visualPoint.Y * _mmToVisualFactorC2;
            return visualPoint;
        }

        protected override object createContent()
        {
            throw new NotImplementedException();
        }
    }
}
