using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using System.Runtime.Serialization;


namespace ControllerCNC.GUI
{
    [Serializable]
    abstract class ShapeItem : PointProviderItem
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        private readonly IEnumerable<Point4Df> _shapeDefinition;

        /// <summary>
        /// First facet of the shape.
        /// </summary>
        protected PlaneShape ShapeUV { get; private set; }

        /// <summary>
        /// Second facet of the shape.
        /// </summary>
        protected PlaneShape ShapeXY { get; private set; }

        protected double _shapeMaxC1 { get { return Math.Max(ShapeUV.MaxC1, ShapeXY.MaxC1); } }

        protected double _shapeMaxC2 { get { return Math.Max(ShapeUV.MaxC2, ShapeXY.MaxC2); } }

        protected double _shapeMinC1 { get { return Math.Min(ShapeUV.MinC1, ShapeXY.MinC1); } }

        protected double _shapeMinC2 { get { return Math.Min(ShapeUV.MinC2, ShapeXY.MinC2); } }

        /// <summary>
        /// Definition of the shape.
        /// </summary>
        protected IEnumerable<Point4Df> ShapeDefinition { get { return _shapeDefinition; } }

        /// <summary>
        /// Determine size of the shape in milimeters.
        /// </summary>
        private Size _shapeMetricSize;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _stepToVisualFactorC1;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        private double _stepToVisualFactorC2;

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
        /// Clones the shape item.
        /// </summary>
        internal abstract ShapeItem Clone(ReadableIdentifier cloneName);

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
                _shapeMetricSize = new Size(value, value * (_shapeMaxC2 - _shapeMinC2) / (_shapeMaxC1 - _shapeMinC1));
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
                _shapeMetricSize = new Size(value * (_shapeMaxC1 - _shapeMinC1) / (_shapeMaxC2 - _shapeMinC2), value);
                fireOnSettingsChanged();
            }
        }

        /// </inheritdoc>
        internal override IEnumerable<Point4D> ItemPoints
        {
            get
            {
                var ratioC1 = 1.0 * _shapeMaxC1 - _shapeMinC1;
                if (ratioC1 == 0)
                    throw new NotImplementedException("Cannot stretch the width");

                var ratioC2 = 1.0 * _shapeMaxC2 - _shapeMinC2;
                if (ratioC2 == 0)
                    throw new NotImplementedException("Cannot stretch the width");

                foreach (var definitionPoint in _shapeDefinition)
                {
                    var point = rotate(definitionPoint);

                    var u = (int)Math.Round((point.U - _shapeMinC1) / ratioC1 * _shapeMetricSize.Width / Constants.MilimetersPerStep);
                    var v = (int)Math.Round((point.V - _shapeMinC2) / ratioC2 * _shapeMetricSize.Height / Constants.MilimetersPerStep);
                    var x = (int)Math.Round((point.X - _shapeMinC1) / ratioC1 * _shapeMetricSize.Width / Constants.MilimetersPerStep);
                    var y = (int)Math.Round((point.Y - _shapeMinC2) / ratioC2 * _shapeMetricSize.Height / Constants.MilimetersPerStep);
                    yield return new Point4D(u + PositionC1, v + PositionC2, x + PositionC1, y + PositionC2);
                }
            }
        }

        internal ShapeItem(ReadableIdentifier name, IEnumerable<Point4Df> shapeDefinition)
            : base(name)
        {
            if (shapeDefinition == null)
                throw new ArgumentNullException("shapeDefinition");

            _shapeDefinition = shapeDefinition.ToArray();
            constructionInitialization();
        }

        internal ShapeItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shapeDefinition = (Point4Df[])info.GetValue("_shapeDefinition", typeof(Point4Df[]));
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            _rotationAngle = info.GetDouble("_rotationAngle");
            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_rotationAngle", _rotationAngle);
        }

        /// <summary>
        /// Sets size of the shape to original size (given by definition)
        /// </summary>
        internal virtual void SetOriginalSize()
        {
            var c1Diff = _shapeMaxC1 - _shapeMinC1;
            var c2Diff = _shapeMaxC2 - _shapeMinC2;
            if (c1Diff > c2Diff)
                MetricWidth = c1Diff * Constants.MilimetersPerStep;
            else
                MetricHeight = c2Diff * Constants.MilimetersPerStep;
        }

        protected virtual void constructionInitialization()
        {
            ShapeUV = new PlaneShape(_shapeDefinition.Select(p => p.ToUV()));
            ShapeXY = new PlaneShape(_shapeDefinition.Select(p => p.ToXY()));

            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = null;

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
            _stepToVisualFactorC1 = size.Width / workspace.StepCountX;
            _stepToVisualFactorC2 = size.Height / workspace.StepCountY;

            Width = _shapeMetricSize.Width / Constants.MilimetersPerStep * _stepToVisualFactorC1;
            Height = _shapeMetricSize.Height / Constants.MilimetersPerStep * _stepToVisualFactorC2;
        }


        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            return arrangeBounds;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            throw new NotImplementedException("Method has be overriden");
        }

        /// <summary>
        /// Creates figure by joining the given points.
        /// </summary>
        protected PathFigure CreatePathFigure(IEnumerable<Point2D> geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var isFirst = true;
            var firstPoint = new Point(0, 0);
            foreach (var point in geometryPoints)
            {
                var planePoint = new Point(point.C1 - PositionC1, point.C2 - PositionC2);
                planePoint.X = planePoint.X * _stepToVisualFactorC1;
                planePoint.Y = planePoint.Y * _stepToVisualFactorC2;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                if (isFirst)
                    firstPoint = planePoint;
                isFirst = false;
            }

            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }

        /// <summary>
        /// Rotates given point according to current rotation angle.
        /// </summary>
        protected Point4Df rotate(Point4Df point)
        {
            var c1 = _shapeMinC1 + _shapeMaxC1 / 2.0;
            var c2 = _shapeMinC2 + _shapeMaxC2 / 2.0;

            var centeredU = point.U - c1;
            var centeredV = point.V - c2;
            var centeredX = point.X - c1;
            var centeredY = point.Y - c2;

            var rotatedU = centeredU * _rotationCos - centeredV * _rotationSin;
            var rotatedV = centeredV * _rotationCos + centeredU * _rotationSin;
            var rotatedX = centeredX * _rotationCos - centeredY * _rotationSin;
            var rotatedY = centeredY * _rotationCos + centeredX * _rotationSin;
            return new Point4Df(
                rotatedU + c1, rotatedV + c2,
                rotatedX + c1, rotatedY + c2
                );
        }
    }
}
