using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ControllerCNC.GUI
{
    class TrajectoryShapeItem : WorkspaceItem
    {
        /// <summary>
        /// Trajectory of the shape.
        /// </summary>
        private readonly Trajectory4D _trajectory;

        private readonly WorkspacePanel _workspace;

        private readonly int _xStepOffset;

        private readonly int _yStepOffset;

        internal IEnumerable<Point4D> TrajectoryPoints
        {
            get
            {
                return _trajectory.Points.Select(p => new Point4D(p.U, p.V, p.X - _xStepOffset + PositionX, p.Y - _yStepOffset + PositionY));
            }
        }

        internal TrajectoryShapeItem(Trajectory4D trajectory, WorkspacePanel workspace)
        {
            if (trajectory == null)
                throw new ArgumentNullException("trajectory");

            if (workspace == null)
                throw new ArgumentNullException("workspace");

            //Background = Brushes.Red;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = Brushes.Transparent;


            _trajectory = trajectory;
            _workspace = workspace;

            foreach (var point in _trajectory.Points)
            {
                _xStepOffset = Math.Min(point.X, _xStepOffset);
                _yStepOffset = Math.Min(point.Y, _yStepOffset);
            }

            initialize();

            _workspace.Children.Add(this);
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            var pathSegments = new PathSegmentCollection();

            var isFirst = true;

            var workWidth = Math.Floor(_workspace.Size.Width);
            var workHeight = Math.Floor(_workspace.Size.Height);
            foreach (var point in _trajectory.Points)
            {
                var planePoint = getNormalizedPlanePoint(point);
                planePoint.X = planePoint.X * workWidth;
                planePoint.Y = planePoint.Y * workHeight;

                if (planePoint.X > ActualWidth)
                    planePoint.X = planePoint.X;
                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                isFirst = false;
            }

            var path = new Path();
            var figure = new PathFigure(new Point(0, 0), pathSegments, false);
            path.Data = new PathGeometry(new[] { figure });
            path.Stroke = Brushes.Black;
            path.StrokeThickness = 1.0;

            return path;
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            var width = 0.0;
            var height = 0.0;
            foreach (var point in _trajectory.Points)
            {
                var planePoint = getNormalizedPlanePoint(point);
                width = Math.Max(planePoint.X, width);
                height = Math.Max(planePoint.Y, height);
            }


            Height = height * _workspace.Size.Height;
            Width = width * _workspace.Size.Width;

            base.MeasureOverride(constraint);

            return new Size(Height, Width);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Content = createContent();
            base.ArrangeOverride(arrangeBounds);
            return new Size(Height, Width);
        }

        /// <summary>
        /// Gets point in plane represented by current shape.
        /// </summary>
        private Point getNormalizedPlanePoint(Point4D point)
        {
            var x = (1.0 * point.X - _xStepOffset) / _workspace.StepCountX;
            var y = (1.0 * point.Y - _yStepOffset) / _workspace.StepCountY;
            if (x < 0 || y < 0)
                throw new NotSupportedException();
            return new Point(x, y);
        }
    }
}
