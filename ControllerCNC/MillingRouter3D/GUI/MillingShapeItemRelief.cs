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
using ControllerCNC.GUI;
using System.Windows.Media.Imaging;

namespace MillingRouter3D.GUI
{
    [Serializable]
    class MillingShapeItemRelief : MillingItem
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        private readonly double[,] _reliefDefinition;

        /// <summary>
        /// Determine size of the shape in milimeters.
        /// </summary>
        private Size _shapeMetricSize;

        /// <summary>
        /// Depth of the milling process.
        /// </summary>
        private double _millingDepth = 0.0;

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
                _shapeMetricSize = new Size(value, value * _height / _width);
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
                _shapeMetricSize = new Size(value * _width / _height, value);
                fireOnSettingsChanged();
            }
        }


        internal double MillingDepth
        {
            get
            {
                return _millingDepth;
            }

            set
            {
                if (value == _millingDepth)
                    return;
                _millingDepth = value;
                fireOnSettingsChanged();
            }
        }

        private WriteableBitmap visualization;

        /// <summary>
        /// Brush for the item fill.
        /// </summary>
        private Brush _itemBrush;

        /// <summary>
        /// Pen for item border.
        /// </summary>
        private Pen _itemPen;

        /// <summary>
        /// Pen for the cut
        /// </summary>
        private Pen _cutPen = new Pen();

        private HeightMapShape _shapeMap;

        private int _width;

        private int _height;

        internal MillingShapeItemRelief(ReadableIdentifier name, double[,] reliefDefinition)
            : base(name)
        {
            if (reliefDefinition == null)
                throw new ArgumentNullException("reliefDefinition");

            _reliefDefinition = (double[,])reliefDefinition.Clone();
            _millingDepth = 1.0;

            constructionInitialization();
        }

        internal MillingShapeItemRelief(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _reliefDefinition = (double[,])info.GetValue("_reliefDefinition", typeof(double[,]));
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            _millingDepth = info.GetDouble("_millingDepth");

            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_reliefDefinition", _reliefDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_millingDepth", _millingDepth);
        }

        /// <summary>
        /// Sets size of the shape to original size (given by definition)
        /// </summary>
        internal virtual void SetOriginalSize()
        {
            if (_width > _height)
                MetricWidth = _width;
            else
                MetricHeight = _height;
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            //the rendering is controlled directly by current object
            return null;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            return arrangeBounds;
        }

        protected virtual void constructionInitialization()
        {
            _width = _reliefDefinition.GetLength(0);
            _height = _reliefDefinition.GetLength(1);
            _width = 50;
            _height = 50;
            _millingDepth = 3;
            SetOriginalSize();

            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = null;

            initialize();

            _itemBrush = new SolidColorBrush(Colors.LightGray);
            _itemBrush.Opacity = 0.4;

            _cutPen = new Pen(Brushes.Blue, 2.0);
            _cutPen.DashStyle = DashStyles.Dot;

            _itemPen = new Pen(Brushes.Black, 1.0);
            _shapeMap = new HeightMapShape(_reliefDefinition);
            refreshVisualization();
        }


        private void refreshVisualization()
        {
            visualization = new WriteableBitmap(_width, _height, _width, _height, PixelFormats.Gray8, null);
            var pixels = new byte[visualization.PixelHeight * visualization.PixelWidth * visualization.Format.BitsPerPixel / 8];

            var resolutionX = _width;
            var resolutionY = _height;
            for (var xi = 0; xi < resolutionX; ++xi)
            {
                for (var yi = 0; yi < resolutionY; ++yi)
                {
                    var xRatio = 1.0 * xi / resolutionX;
                    var yRatio = 1.0 * yi / resolutionY;

                    var depth = _shapeMap.GetHeight(xRatio, yRatio);
                    var visualX = _shapeMetricSize.Width * _mmToVisualFactorC1 * xRatio;
                    var visualY = _shapeMetricSize.Height * _mmToVisualFactorC2 * yRatio;
                    var visualPoint = new Point(visualX, visualY);

                    var colorIntensity = (byte)Math.Round(255 * depth);
                    var index = (yi * visualization.PixelWidth + xi) * visualization.Format.BitsPerPixel / 8;
                    pixels[index] = colorIntensity;
                }
            }

            visualization.WritePixels(
                    new Int32Rect(0, 0, visualization.PixelWidth, visualization.PixelHeight),
                    pixels,
                    visualization.PixelWidth * visualization.Format.BitsPerPixel / 8,
                    0);

            visualization.Freeze();
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var startPoint = new Point2Dmm(PositionX, PositionY);
            var endPoint = new Point2Dmm(PositionX + MetricWidth, PositionY + MetricHeight);

            drawingContext.DrawImage(visualization, new Rect(ConvertToVisual(startPoint), ConvertToVisual(endPoint)));
        }

        /// <summary>
        /// Rotates given point according to current rotation angle.
        /// </summary>
        protected Point3Dmm rotate(Point3Dmm point)
        {
            var c1 = 0.5;
            var c2 = 0.5;

            var centeredX = point.X - c1;
            var centeredY = point.Y - c2;

            var rotatedX = centeredX * _rotationCos - centeredY * _rotationSin;
            var rotatedY = centeredY * _rotationCos + centeredX * _rotationSin;
            return new Point3Dmm(
                rotatedX + c1, rotatedY + c2, point.Z
                );
        }

        internal MillingShapeItemRelief Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new MillingShapeItemRelief(cloneName, _reliefDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            shapeItem.MillingDepth = MillingDepth;
            return shapeItem;
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            builder.GotoZeroLevel();

            var maxDepth = _millingDepth;
            var upDown = true;
            var stepLength = 1.0;
            var resolutionX = _shapeMetricSize.Width / stepLength;
            var resolutionY = _shapeMetricSize.Height / stepLength;
            for (var xi = 0.0; xi < resolutionX; xi += stepLength)
            {
                for (var yi = 0.0; yi < resolutionY; yi += stepLength)
                {
                    var xRatio = 1.0 * xi / resolutionX;
                    var yRatio = 1.0 * yi / resolutionY;

                    var depth = _shapeMap.GetHeight(xRatio, yRatio) * _millingDepth;
                    var x = _shapeMetricSize.Width * xRatio;
                    var y = _shapeMetricSize.Height * yRatio;
                    if (!upDown)
                        y = _shapeMetricSize.Height - y;

                    var point = new Point3Dmm(PositionX + x, PositionY + y, depth);
                    builder.AddCuttingSpeedTransition(new Point2Dmm(point.X, point.Y), point.Z);
                }
                upDown = !upDown;
            }

            builder.GotoTransitionLevel();
            builder.AddRampedLine(EntryPoint);
        }

        protected override Point2Dmm getEntryPoint()
        {
            return new Point2Dmm(PositionX, PositionY);
        }
    }
}
