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
    abstract class MillingItem : PermanentMillingWorkspaceItem
    {
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

        protected override object createContent()
        {
            throw new NotImplementedException();
        }
    }
}
