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

        private readonly int _xStepMin;

        private readonly int _yStepMin;

        private readonly int _xStepMax;

        private readonly int _yStepMax;

        private Size _workspaceSize;

        internal IEnumerable<Point4D> TrajectoryPoints
        {
            get
            {
                return _trajectory.Points.Select(p => new Point4D(p.U, p.V, p.X - _xStepMin + PositionX, p.Y - _yStepMin + PositionY));
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

            _xStepMin = _yStepMin = int.MaxValue;
            _xStepMax = _xStepMax = int.MinValue;

            foreach (var point in _trajectory.Points)
            {
                _xStepMin = Math.Min(point.X, _xStepMin);
                _yStepMin = Math.Min(point.Y, _yStepMin);
                _xStepMax = Math.Max(point.X, _xStepMax);
                _yStepMax = Math.Max(point.Y, _yStepMax);
            }

            initialize();

            _workspace.Children.Add(this);
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            return null;
        }

        /// <inheritdoc/>
        internal override void RegisterWorkspaceSize(Size size)
        {
            var widthStep = _xStepMax - _xStepMin;
            var heightStep = _yStepMax - _yStepMin;


            Width = widthStep * size.Width / _workspace.StepCountX;
            Height = heightStep * size.Height / _workspace.StepCountY;
            _workspaceSize = size;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var pathSegments = new PathSegmentCollection();

            var isFirst = true;

            var workWidth = Math.Floor(_workspaceSize.Width);
            var workHeight = Math.Floor(_workspaceSize.Height);
            foreach (var point in _trajectory.Points)
            {
                var planePoint = getNormalizedPlanePoint(point);
                planePoint.X = planePoint.X * workWidth;
                planePoint.Y = planePoint.Y * workHeight;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                isFirst = false;
            }

            var figure = new PathFigure(new Point(0, 0), pathSegments, false);
            var geometry = new PathGeometry(new[] { figure });
            var pen = new Pen(Brushes.Black, 1.0);
            drawingContext.DrawGeometry(Brushes.Transparent, pen, geometry);
        }

        /// <summary>
        /// Gets point in plane represented by current shape.
        /// </summary>
        private Point getNormalizedPlanePoint(Point4D point)
        {
            var x = (1.0 * point.X - _xStepMin) / _workspace.StepCountX;
            var y = (1.0 * point.Y - _yStepMin) / _workspace.StepCountY;
            if (x < 0 || y < 0)
                throw new NotSupportedException();
            return new Point(x, y);
        }
    }
}
