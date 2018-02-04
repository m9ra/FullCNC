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
        /// Determine whether explicit kerf will be used for cutting.
        /// </summary>
        private bool _useExplicitKerf;

        /// <summary>
        /// Sets kerf for XY facet.
        /// </summary>
        private double _kerfXY;

        /// <summary>
        /// Determine whether <see cref="KerfUV"/> and <see cref="KerfXY"/> will be used.
        /// </summary>
        internal bool UseExplicitKerf
        {
            get
            {
                return _useExplicitKerf;
            }

            set
            {
                if (value == _useExplicitKerf)
                    return;

                _useExplicitKerf = value;
                fireOnSettingsChanged();
            }
        }

        /// <summary>
        /// Explicit kerf for XY.
        /// </summary>
        internal double KerfXY
        {
            get
            {
                return UseExplicitKerf ? _kerfXY : 0;
            }

            set
            {
                if (value == _kerfXY || !UseExplicitKerf)
                    return;

                _kerfXY = value;
                fireOnSettingsChanged();
            }
        }

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

        internal bool UseClockwiseCut
        {
            get
            {
                return _useClockwiseCut;
            }

            set
            {
                if (value == _useClockwiseCut)
                    return;

                _useClockwiseCut = value;
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

        /// <inheritdoc/>
        internal IEnumerable<Point2Dmm[]> CutPoints
        {
            get { return TransformedShapeDefinitionWithKerf.Select(s => translateToWorkspace(s).ToArray()); }
        }

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

        internal IEnumerable<Point2Dmm[]> TransformedShapeDefinitionWithKerf
        {
            get
            {
                return TransformedShapeDefinition.Select(s => addKerf(s).ToArray());
            }
        }

        internal MillingShapeItem2D(ReadableIdentifier name, IEnumerable<Point2Dmm[]> shapeDefinition)
            : base(name)
        {
            if (shapeDefinition == null)
                throw new ArgumentNullException("shapeDefinition");

            _shapeDefinition = shapeDefinition.ToArray();
            _useClockwiseCut = true;

            constructionInitialization();
        }

        internal MillingShapeItem2D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shapeDefinition = (Point2Dmm[][])info.GetValue("_shapeDefinition", typeof(Point2Dmm[][]));
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            _rotationAngle = info.GetDouble("_rotationAngle");
            _useExplicitKerf = info.GetBoolean("_useExplicitKerf");
            _kerfXY = info.GetDouble("_kerfXY");
            _useClockwiseCut = info.GetBoolean("_useClockwiseCut");

            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_rotationAngle", _rotationAngle);
            info.AddValue("_useExplicitKerf", _useExplicitKerf);
            info.AddValue("_kerfXY", _kerfXY);
            info.AddValue("_useClockwiseCut", _useClockwiseCut);
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


        /// <inheritdoc/>
        protected IEnumerable<Point2Dmm> translateToWorkspace(IEnumerable<Point2Dmm> points)
        {
            foreach (var point in points)
            {
                yield return new Point2Dmm(point.C1 + PositionX, point.C2 + PositionY);
            }
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
                    point = new Point2Dmm(x, y);
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
            }
            Shapes = shapes.ToArray();

            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Background = null;

            //reset rotation
            var desiredAngle = _rotationAngle;
            RotationAngle = desiredAngle + 1;
            RotationAngle = desiredAngle;
            initialize();

            _itemBrush = new SolidColorBrush(Colors.LightGray);
            _itemBrush.Opacity = 0.4;

            _cutPen = new Pen(Brushes.Blue, 2.0);
            _cutPen.DashStyle = DashStyles.Dot;

            _itemPen = new Pen(Brushes.Black, 1.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var workspace = Parent as MillingWorkspacePanel;
            if (workspace != null && workspace.CuttingKerf != 0.0)
            {
                throw new NotImplementedException();
            }

            var itemPoints = TransformedShapeDefinition.Select(s => translateToWorkspace(s).ToArray()).ToArray();
            var geometry = CreatePathFigure(itemPoints);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);

            for (var i = 0; i < itemPoints.Length && itemPoints.Length > 1; ++i)
            {
                var shape1EndPoint = itemPoints[i].Last();
                var shape2EntryPoint = itemPoints[(i + 1) % itemPoints.Length].First();
                drawingContext.DrawLine(_cutPen, ConvertToVisual(shape1EndPoint), ConvertToVisual(shape2EntryPoint));
            }
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


        protected Vector calculateKerfShift(Point2Dmm p1, Point2Dmm p2, Point2Dmm p3, double kerf)
        {
            var v1 = diffVector(p1, p2);
            var v2 = diffVector(p2, p3);

            var nV1 = new Vector(v1.Y, -v1.X);
            var nV2 = new Vector(v2.Y, -v2.X);

            if (nV1.Length == 0)
                nV1 = nV2;

            if (nV2.Length == 0)
                nV2 = nV1;

            nV1.Normalize();
            nV2.Normalize();

            var shift = (nV1 + nV2) * kerf / 2;
            return shift;
        }

        protected Vector diffVector(Point2Dmm p1, Point2Dmm p2)
        {
            var diff12_C1 = p2.C1 - p1.C1;
            var diff12_C2 = p2.C2 - p1.C2;

            return new Vector(diff12_C1, diff12_C2);
        }

        protected IEnumerable<Point2Dmm> addKerf(IEnumerable<Point2Dmm> points)
        {
            var workspace = Parent as MillingWorkspacePanel;
            if (workspace == null || (workspace.CuttingKerf == 0.0 && !this.UseExplicitKerf))
                //there is no change
                return points;


            var result = applyKerf(points, workspace);
            return result;
        }

        protected Point2Dmm[] applyKerf(IEnumerable<Point2Dmm> points, MillingWorkspacePanel workspace)
        {
            var pointsArr = points.ToArray();

            var result = new List<Point2Dmm>();
            for (var i = 0; i < pointsArr.Length; ++i)
            {
                var prevPoint = getNextDifferent(pointsArr, i, -1);
                var point = pointsArr[i];
                var nextPoint = getNextDifferent(pointsArr, i, 1);

                result.Add(applyKerf(prevPoint, point, nextPoint, workspace));
            }

            return result.ToArray();
        }

        protected Point2Dmm applyKerf(Point2Dmm p1, Point2Dmm p2, Point2Dmm p3, MillingWorkspacePanel workspace)
        {
            var kerf = reCalculateKerf(workspace.CuttingKerf);
            var shift = calculateKerfShift(p1, p2, p3, kerf);
            return new Point2Dmm(p2.C1 + shift.X, p2.C2 + shift.Y);
        }

        private Point2Dmm getNextDifferent(Point2Dmm[] points, int startIndex, int increment)
        {
            var startPointXY = points[startIndex];

            //find XY next
            var i = (startIndex + increment + points.Length) % points.Length;
            while (points[i].Equals(startPointXY))
            {
                i = (i + increment + points.Length) % points.Length;
            }
            var endPointXY = points[i];

            return endPointXY;
        }

        protected double reCalculateKerf(double kerf)
        {
            if (!_useClockwiseCut)
                kerf *= -1;

            return kerf;
        }

        internal MillingShapeItem2D Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new MillingShapeItem2D(cloneName, ShapeDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            return shapeItem;
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            foreach (var cluster in CutPoints)
            {
                builder.GotoTransitionLevel();
                builder.AddRampedLine(cluster[0], builder.PlaneAcceleration, builder.TransitionSpeed);

                builder.GotoZ(2.0);
                foreach (var point in cluster)
                {
                    builder.AddConstantSpeedTransition(point, workspace.CuttingSpeed);
                }
            }

            builder.GotoTransitionLevel();
            builder.AddRampedLine(EntryPoint, builder.PlaneAcceleration, builder.TransitionSpeed);
        }

        protected override Point2Dmm getEntryPoint()
        {
            return CutPoints.First()[0];
        }
    }
}
