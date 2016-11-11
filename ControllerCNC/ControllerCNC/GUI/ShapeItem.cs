using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using System.Runtime.Serialization;

namespace ControllerCNC.GUI
{
    [Serializable]
    class ShapeItem : PointProviderItem
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        private readonly Point[] _shapeDefinition;

        private double _shapeMinX;

        private double _shapeMinY;

        private double _shapeMaxX;

        private double _shapeMaxY;

        /// <summary>
        /// Determine size of the shape in milimeters.
        /// </summary>
        private Size _shapeMetricSize;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _xStepToVisualFactor;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _yStepToVisualFactor;

        internal double MetricWidth
        {
            get
            {
                return _shapeMetricSize.Width;
            }

            set
            {
                if (value == _shapeMetricSize.Width)
                    return;
                _shapeMetricSize = new Size(value, value * (_shapeMaxY - _shapeMinY) / (_shapeMaxX - _shapeMinX));
                fireOnSettingsChanged();
            }
        }

        internal double MetricHeight
        {
            get
            {
                return _shapeMetricSize.Height;
            }

            set
            {
                if (value == _shapeMetricSize.Width)
                    return;
                _shapeMetricSize = new Size(value * (_shapeMaxX - _shapeMinX) / (_shapeMaxY - _shapeMinY), value);
                fireOnSettingsChanged();
            }
        }

        internal override IEnumerable<Point4D> ItemPoints
        {
            get
            {
                var ratioX = _shapeMaxX - _shapeMinX;
                if (ratioX == 0)
                    throw new NotImplementedException("Cannot stretch the width");

                var ratioY = _shapeMaxY - _shapeMinY;
                if (ratioY == 0)
                    throw new NotImplementedException("Cannot stretch the width");

                foreach (var point in _shapeDefinition)
                {
                    var x = (int)Math.Round((point.X - _shapeMinX) / ratioX * _shapeMetricSize.Width / Constants.MilimetersPerStep);
                    var y = (int)Math.Round((point.Y - _shapeMinY) / ratioY * _shapeMetricSize.Height / Constants.MilimetersPerStep);
                    yield return new Point4D(0, 0, x + PositionX, y + PositionY);
                }
            }
        }

        internal ShapeItem(string name, IEnumerable<Point> shapeDefinition)
            : base(name)
        {
            if (shapeDefinition == null)
                throw new ArgumentNullException("trajectory");

            _shapeDefinition = shapeDefinition.ToArray();
            constructionInitialization();
        }

        internal ShapeItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shapeDefinition = (Point[])info.GetValue("_shapeDefinition", typeof(Point[]));
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            constructionInitialization();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
        }

        internal void constructionInitialization()
        {
            _shapeMinX = _shapeMinY = double.PositiveInfinity;
            _shapeMaxX = _shapeMaxX = double.NegativeInfinity;

            foreach (var point in _shapeDefinition)
            {
                _shapeMinX = Math.Min(point.X, _shapeMinX);
                _shapeMinY = Math.Min(point.Y, _shapeMinY);
                _shapeMaxX = Math.Max(point.X, _shapeMaxX);
                _shapeMaxY = Math.Max(point.Y, _shapeMaxY);
            }

            //Background = Brushes.Red;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = Brushes.Transparent;

            initialize();
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            //the rendering is controlled directly by current object
            return null;
        }

        /// <inheritdoc/>
        internal override void RecalculateToWorkspace(WorkspacePanel workspace, Size size)
        {
            _xStepToVisualFactor = size.Width / workspace.StepCountX;
            _yStepToVisualFactor = size.Height / workspace.StepCountY;

            Width = _shapeMetricSize.Width / Constants.MilimetersPerStep * _xStepToVisualFactor;
            Height = _shapeMetricSize.Height / Constants.MilimetersPerStep * _yStepToVisualFactor;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var pathSegments = new PathSegmentCollection();

            var isFirst = true;

            foreach (var point in ItemPoints)
            {
                var planePoint = new Point(point.X - PositionX, point.Y - PositionY);
                planePoint.X = planePoint.X * _xStepToVisualFactor;
                planePoint.Y = planePoint.Y * _yStepToVisualFactor;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                isFirst = false;
            }

            var figure = new PathFigure(new Point(0, 0), pathSegments, false);
            var geometry = new PathGeometry(new[] { figure });
            var pen = new Pen(Brushes.Black, 1.0);
            drawingContext.DrawGeometry(Brushes.Transparent, pen, geometry);
        }

    }
}
