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

        /// <summary>
        /// Angle of rotation [0..360)degree
        /// </summary>
        private double _rotationAngle;

        /// <summary>
        /// Sin of the current rotation.
        /// </summary>
        private double _rotationSin;

        /// <summary>
        /// Cos of the current rotation.
        /// </summary>
        private double _rotationCos;

        /// <summary>
        /// Brush for the item fill.
        /// </summary>
        private Brush _itemBrush;

        /// <summary>
        /// Rotation in degrees.
        /// </summary>
        internal double RotationAngle
        {
            get
            {
                return _rotationAngle;
            }

            set
            {
                if (value == _rotationAngle)
                    return;

                _rotationAngle = value;
                _rotationSin = Math.Sin(_rotationAngle / 360 * 2 * Math.PI);
                _rotationCos = Math.Cos(_rotationAngle / 360 * 2 * Math.PI);
                fireOnSettingsChanged();
            }
        }

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

                foreach (var definitionPoint in _shapeDefinition)
                {
                    var point = rotate(definitionPoint);
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
            _rotationAngle = info.GetDouble("_rotationAngle");
            constructionInitialization();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_rotationAngle", _rotationAngle);
        }

        internal ShapeItem Clone()
        {
            var shapeItem = new ShapeItem(Name, _shapeDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            return shapeItem;
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
            Background = null;
            _itemBrush = new SolidColorBrush(Colors.LightGray);
            _itemBrush.Opacity = 0.4;

            //reset rotation
            var desiredAngle = _rotationAngle;
            RotationAngle = desiredAngle + 1;
            RotationAngle = desiredAngle;
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
            var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
            var pen = new Pen(Brushes.Black, 1.0);
            drawingContext.DrawGeometry(_itemBrush, pen, geometry);
        }

        /// <summary>
        /// Rotates given point according to current rotation angle.
        /// </summary>
        private Point rotate(Point point)
        {
            var cX = _shapeMinX + _shapeMaxX / 2;
            var cY = _shapeMinY + _shapeMaxY / 2;
            var centeredX = point.X - cX;
            var centeredY = point.Y - cY;

            var rotatedX = centeredX * _rotationCos - centeredY * _rotationSin;
            var rotatedY = centeredY * _rotationCos + centeredX * _rotationSin;
            return new Point(rotatedX + cX, rotatedY + cY);
        }
    }
}
