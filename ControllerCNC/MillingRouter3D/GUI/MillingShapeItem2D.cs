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
    internal class MillingShapeItem2D : MillingItem
    {
        /// <summary>
        /// Points defining the shape.
        /// </summary>
        private readonly Point2Dmm[][] _shapeDefinition;

        private IEnumerable<Point2Dmm[]> _currentOffsetLines = null;

        internal IEnumerable<PlaneShape> Shapes { get; private set; }

        internal IEnumerable<Point2Dmm[]> OffsetLines
        {
            get
            {
                if (_currentOffsetLines == null)
                    refreshOfffsetLines();

                return afterScaleTransformation(_currentOffsetLines);
            }
        }

        private double maxC1 => Shapes.Select(s => s.MaxC1).Max();

        private double maxC2 => Shapes.Select(s => s.MaxC2).Max();

        private double minC1 => Shapes.Select(s => s.MinC1).Min();

        private double minC2 => Shapes.Select(s => s.MinC2).Min();

        /// <summary>
        /// Determine whether cut in a clockwise direction will be used.
        /// </summary>
        private readonly bool _useClockwiseCut;

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
                _currentOffsetLines = null;
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
                _currentOffsetLines = null;
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

        private void refreshOfffsetLines()
        {
            _currentOffsetLines = scaledOnlyDefinitionTransformation(_shapeDefinition);
            /*/   var offsetClusters = new List<Point2Dmm[]>();
               var remainingClusters = new HashSet<Point2Dmm[]>(_currentOffsetLines);
               foreach (var cluster in remainingClusters.ToArray())
               {
                   var offsetCalculator = new OffsetCalculator(cluster.Reverse());
                   var offsetPoints = offsetCalculator.WithOffset(-1.0);
                   if (offsetPoints.Any())
                   {
                       offsetClusters.AddRange(offsetPoints);
                   }
                   else
                   {
                       remainingClusters.Remove(cluster);
                   }
               }

               _currentOffsetLines = offsetClusters.OrderByDescending(c => c.Select(p => p.C2).Min()).ToArray();/**/

            /**/

            var itemPoints = scaledOnlyDefinitionTransformation(_shapeDefinition);
            itemPoints = OffsetCalculator.Join(itemPoints);
            var offsetClusters = new List<Point2Dmm[]>();
            var remainingClusters = new HashSet<Point2Dmm[]>(itemPoints);
            var toolWidth = 4.0;
            for (var i = 0; i < 10; ++i)
            {
                if (remainingClusters.Count == 0)
                    break;

                foreach (var cluster in remainingClusters.ToArray())
                {
                    var offsetCalculator = new OffsetCalculator(cluster);
                    var offsetPoints = offsetCalculator.WithOffset(toolWidth / 2 * (i + 1));
                    if (offsetPoints.Any())
                    {
                        offsetClusters.AddRange(offsetPoints);
                    }
                    else
                    {
                        remainingClusters.Remove(cluster);
                    }
                }
            }

            _currentOffsetLines = offsetClusters.ToArray();
            //_currentOffsetLines = itemPoints.ToArray();
            //_currentOffsetLines = offsetClusters.OrderByDescending(c => c.Select(p => p.C2).Min()).ToArray();
            /*/
            var itemPoints = scaledOnlyDefinitionTransformation(_shapeDefinition);
            var calculator = new StrokeOffsetCalculator(itemPoints);
            //var offsetClusters = calculator.WithOffset(0.7);
            var offsetClusters = ImageInterpolator.FlattenStokes(itemPoints);
            offsetClusters = itemPoints;
            _currentOffsetLines = offsetClusters.OrderByDescending(c => c.Select(p => p.C2).Min()).ToArray();/**/
        }

        private Point2Dmm[][] preparePoints(IEnumerable<Point2Dmm[]> points)
        {
            points = points.Select(p => p.Distinct().Concat(new[] { p.First() }).ToArray()).ToArray();

            var result = new List<Point2Dmm[]>();
            foreach (var cluster in points)
            {
                if (GeometryUtils.ArePointsClockwise(cluster))
                    result.Add(cluster);
                else
                    result.Add(cluster.Reverse().ToArray());

            }

            return result.ToArray();
        }

        protected IEnumerable<Point2Dmm[]> scaledOnlyDefinitionTransformation(IEnumerable<Point2Dmm[]> pointClusters)
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
                var isClockwise = GeometryUtils.ArePointsClockwise(pointCluster);

                if (_useClockwiseCut != isClockwise)
                    points = points.Reverse().ToArray();

                var result = new List<Point2Dmm>();
                foreach (var point in points)
                {
                    var x = (point.C1 - minC1) / ratioC1 * _shapeMetricSize.Width;
                    var y = (point.C2 - minC2) / ratioC2 * _shapeMetricSize.Height;
                    result.Add(new Point2Dmm(x, y));
                }

                yield return result.ToArray();
            }
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
                var isClockwise = GeometryUtils.ArePointsClockwise(pointCluster);

                if (_useClockwiseCut != isClockwise)
                    points = points.Reverse().ToArray();

                var result = new List<Point2Dmm>();
                foreach (var definitionPoint in points)
                {
                    var point = rotate(definitionPoint, minC1, maxC1, minC2, maxC2);

                    var x = (point.C1 - minC1) / ratioC1 * _shapeMetricSize.Width;
                    var y = (point.C2 - minC2) / ratioC2 * _shapeMetricSize.Height;
                    point = new Point2Dmm(x + PositionX, y + PositionY);
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

            var offsetLines = OffsetLines.ToArray();
            var offsetGeometry = CreatePathFigure(offsetLines);
            drawingContext.DrawGeometry(null, _cutPen, offsetGeometry);
        }

        /// <summary>
        /// Rotates given point according to current rotation angle.
        /// </summary>
        protected Point2Dmm rotate(Point2Dmm point, double minC1, double maxC1, double minC2, double maxC2)
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

        private IEnumerable<Point2Dmm[]> afterScaleTransformation(IEnumerable<Point2Dmm[]> definition)
        {
            var result = new List<Point2Dmm[]>();
            if (!definition.Any())
                return result;

            var minC1 = definition.SelectMany(c => c).Min(p => p.C1);
            var maxC1 = definition.SelectMany(c => c).Max(p => p.C1);

            var minC2 = definition.SelectMany(c => c).Min(p => p.C2);
            var maxC2 = definition.SelectMany(c => c).Max(p => p.C2);

            foreach (var cluster in definition)
            {
                var resultPoints = new List<Point2Dmm>();
                foreach (var point in cluster)
                {
                    var rotatedPoint = rotate(point, minC1, maxC1, minC2, maxC2);
                    var resultPoint = new Point2Dmm(rotatedPoint.C1 + PositionX, rotatedPoint.C2 + PositionY);
                    resultPoints.Add(resultPoint);
                }

                result.Add(resultPoints.ToArray());
            }

            return result;
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
            var offsetLines = afterScaleTransformation(_currentOffsetLines);
            var currentDepth = 0.0;
            while (currentDepth < MillingDepth)
            {
                var depthIncrement = Math.Min(workspace.MaxLayerCut, MillingDepth - currentDepth);
                currentDepth += depthIncrement;

                foreach (var cluster in offsetLines)
                {
                    builder.GotoTransitionLevel();
                    builder.AddRampedLine(cluster[0]);


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
