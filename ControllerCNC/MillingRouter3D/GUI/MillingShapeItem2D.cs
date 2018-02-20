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

namespace MillingRouter3D.GUI
{
    [Serializable]
    class MillingShapeItem2D : MillingItem
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        private readonly Point2Dmm[][] _shapeDefinition;

        internal IEnumerable<PlaneShape> Shapes { get; private set; }

        private double maxC1 => Shapes.Select(s => s.MaxC1).Max();

        private double maxC2 => Shapes.Select(s => s.MaxC2).Max();

        private double minC1 => Shapes.Select(s => s.MinC1).Min();

        private double minC2 => Shapes.Select(s => s.MinC2).Min();

        /// <summary>
        /// Determine whether cut in a clockwise direction will be used.
        /// </summary>
        private bool _useClockwiseCut;

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
                _shapeMetricSize = new Size(value, value * (maxC2 - minC2) / (maxC1 - minC1));
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
                _shapeMetricSize = new Size(value * (maxC1 - minC1) / (maxC2 - minC2), value);
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

        internal IEnumerable<Point2Dmm[]> ShapeDefinition
        {
            get
            {
                return _shapeDefinition;
            }
        }

        internal IEnumerable<Point2Dmm[]> TransformedShapeDefinition
        {
            get
            {
                return definitionTransformation(ShapeDefinition);
            }
        }

        internal MillingShapeItem2D(ReadableIdentifier name, IEnumerable<Point2Dmm[]> shapeDefinition)
            : base(name)
        {
            if (shapeDefinition == null)
                throw new ArgumentNullException("shapeDefinition");

            _shapeDefinition = preparePoints(shapeDefinition);
            _useClockwiseCut = true;
            _millingDepth = 1.0;

            constructionInitialization();
        }


        internal MillingShapeItem2D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shapeDefinition = (Point2Dmm[][])info.GetValue("_shapeDefinition", typeof(Point2Dmm[][]));
            _shapeDefinition = preparePoints(_shapeDefinition);
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            _useClockwiseCut = info.GetBoolean("_useClockwiseCut");
            _millingDepth = info.GetDouble("_millingDepth");

            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_useClockwiseCut", _useClockwiseCut);
            info.AddValue("_millingDepth", _millingDepth);
        }

        /// <summary>
        /// Sets size of the shape to original size (given by definition)
        /// </summary>
        internal virtual void SetOriginalSize()
        {
            var c1Diff = maxC1 - minC1;
            var c2Diff = maxC2 - minC2;
            if (c1Diff > c2Diff)
                MetricWidth = c1Diff;
            else
                MetricHeight = c2Diff;
        }

        private Point2Dmm[][] preparePoints(IEnumerable<Point2Dmm[]> points)
        {
            /*points = new Point2Dmm[][] {
                new Point2Dmm[]
                {
                    new Point2Dmm(0,10),
                    new Point2Dmm(50,5),
                    new Point2Dmm(100,10),
                    new Point2Dmm(100,5),
                    new Point2Dmm(60,0),
                    new Point2Dmm(40,0),
                    new Point2Dmm(0,10),
                }
            };
*/
            /*/
            points = new Point2Dmm[][]
            {
                new Point2Dmm[]
                {
                    new Point2Dmm(0,90),
                    new Point2Dmm(40,100),
                    new Point2Dmm(30,102),
                    new Point2Dmm(70,102),
                    new Point2Dmm(60,100),
                    new Point2Dmm(100,100),
                    new Point2Dmm(90,0),
                    new Point2Dmm(0,0),
                }
            };/**/

            /*/
            points = new Point2Dmm[][]
            {
                new Point2Dmm[]
                {
                    new Point2Dmm(0,100),
                    new Point2Dmm(40,100),
                    new Point2Dmm(40,15),
                    new Point2Dmm(60,15),
                    new Point2Dmm(60,100),
                    new Point2Dmm(100,100),
                    new Point2Dmm(100,0),
                    new Point2Dmm(0,0),
                }
            };/**/

            points = points.Select(p => p.Distinct().Concat(new[] { p.First() }).ToArray()).ToArray();

            var result = new List<Point2Dmm[]>();
            foreach (var cluster in points)
            {
                if (arePointsClockwise(cluster))
                    result.Add(cluster);
                else
                    result.Add(cluster.Reverse().ToArray());

            }

            return result.ToArray();
        }

        private bool arePointsClockwise(IEnumerable<Point2Dmm> definition)
        {
            var points = definition.ToArray();

            var wSum = 0.0;
            for (var i = 1; i < points.Length; ++i)
            {
                var x1 = points[i - 1];
                var x2 = points[i];

                wSum += (x2.C1 - x1.C1) * (x2.C2 + x1.C2);
            }
            var isClockwise = wSum < 0;
            return isClockwise;
        }

        protected IEnumerable<Point2Dmm[]> definitionTransformation(IEnumerable<Point2Dmm[]> pointClusters)
        {
            var ratioC1 = 1.0 * maxC1 - minC1;
            if (ratioC1 == 0)
                ratioC1 = 1;

            var ratioC2 = 1.0 * maxC2 - minC2;
            if (ratioC2 == 0)
                ratioC2 = 1;

            foreach (var pointCluster in pointClusters)
            {
                var points = pointCluster;
                var isClockwise = arePointsClockwise(pointCluster);

                if (_useClockwiseCut != isClockwise)
                    points = points.Reverse().ToArray();

                var result = new List<Point2Dmm>();
                foreach (var definitionPoint in points)
                {
                    var point = rotate(definitionPoint);

                    var x = (point.C1 - minC1) / ratioC1 * _shapeMetricSize.Width;
                    var y = (point.C2 - minC2) / ratioC2 * _shapeMetricSize.Height;
                    point = new Point2Dmm(x + PositionX, y + PositionY);
                    //point = new Point2Dmm(x, y);
                    result.Add(point);
                }

                yield return result.ToArray();
            }
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
            var shapes = new List<PlaneShape>();
            foreach (var pointCluster in ShapeDefinition)
            {
                shapes.Add(new PlaneShape(pointCluster));
                shapes = shapes.OrderBy(s => s.MaxC2).ToList();
            }
            Shapes = shapes.ToArray();

            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = null;

            initialize();

            _itemBrush = new SolidColorBrush(Colors.LightGray);
            _itemBrush.Opacity = 0.4;

            _cutPen = new Pen(Brushes.Blue, 2.0);
            //_cutPen.DashStyle = DashStyles.Dot;

            _itemPen = new Pen(Brushes.Black, 1.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var itemPoints = TransformedShapeDefinition.ToArray();

            var geometry = CreatePathFigure(itemPoints);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);

            var offsetClusters = new List<Point2Dmm[]>();
            /**/
            for (var i = 0; i < 10; ++i)
            {
                foreach (var cluster in itemPoints)
                {
                    var offsetCalculator = new OffsetCalculator(cluster);
                    var offsetPoints = offsetCalculator.WithOffset(1.0 + 1 * i);
                    offsetClusters.AddRange(offsetPoints);
                }
            }

            var offsetGeometry = CreatePathFigure(offsetClusters);
            drawingContext.DrawGeometry(null, _cutPen, offsetGeometry);

            /*/
            for (var i = 0; i < itemPoints.Length && itemPoints.Length > 1; ++i)
            {
                var shape1EndPoint = itemPoints[i].Last();
                var shape2EntryPoint = itemPoints[(i + 1) % itemPoints.Length].First();
                drawingContext.DrawLine(_cutPen, ConvertToVisual(shape1EndPoint), ConvertToVisual(shape2EntryPoint));
            }/**/
        }

        /// <summary>
        /// Rotates given point according to current rotation angle.
        /// </summary>
        protected Point2Dmm rotate(Point2Dmm point)
        {
            var c1 = minC1 + maxC1 / 2.0;
            var c2 = minC2 + maxC2 / 2.0;

            var centeredX = point.C1 - c1;
            var centeredY = point.C2 - c2;

            var rotatedX = centeredX * _rotationCos - centeredY * _rotationSin;
            var rotatedY = centeredY * _rotationCos + centeredX * _rotationSin;
            return new Point2Dmm(
                rotatedX + c1, rotatedY + c2
                );
        }

        internal MillingShapeItem2D Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new MillingShapeItem2D(cloneName, ShapeDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            shapeItem.MillingDepth = MillingDepth;
            return shapeItem;
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            foreach (var cluster in TransformedShapeDefinition)
            {
                builder.GotoTransitionLevel();
                builder.AddRampedLine(cluster[0]);

                var currentDepth = 0.0;
                while (currentDepth < MillingDepth)
                {
                    var depthIncrement = Math.Min(workspace.MaxLayerCut, MillingDepth - currentDepth);
                    currentDepth += depthIncrement;
                    builder.GotoZ(currentDepth);
                    foreach (var point in cluster)
                    {
                        builder.AddCuttingSpeedTransition(point);
                    }
                }
            }

            builder.GotoTransitionLevel();
            builder.AddRampedLine(EntryPoint);
        }

        protected override Point2Dmm getEntryPoint()
        {
            return TransformedShapeDefinition.First()[0];
        }
    }
}
