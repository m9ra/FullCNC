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
        protected readonly Point4Dmm[] _shapeDefinition;

        /// <summary>
        /// Whether axes are switched with each other.
        /// </summary>
        private bool _isUvXySwitched;

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
        internal IEnumerable<Point4Dmm> ShapeDefinition { get { return _shapeDefinition; } }

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
        /// Defines whether shape is clockwise.
        /// </summary>
        protected bool _isClockwise;

        /// <summary>
        /// Determine whether explicit kerf will be used for cutting.
        /// </summary>
        private bool _useExplicitKerf;

        /// <summary>
        /// Sets kerf for UV facet.
        /// </summary>
        private double _kerfUV;

        /// <summary>
        /// Sets kerf for XY facet.
        /// </summary>
        private double _kerfXY;

        /// <summary>
        /// Clones the shape item.
        /// </summary>
        internal abstract ShapeItem Clone(ReadableIdentifier cloneName);

        /// <summary>
        /// Calculates kerf on the p2 point.
        /// </summary>
        /// <param name="p1">Preceding point.</param>
        /// <param name="p2">Kerfed point.</param>
        /// <param name="p3">Following point.</param>
        /// <param name="workspace">Workspace defining the kerf.</param>
        /// <returns></returns>
        protected abstract Point4Dmm applyKerf(Point4Dmm p1, Point4Dmm p2, Point4Dmm p3, WorkspacePanel workspace);

        /// <summary>
        /// Whether axes are switched with each other.
        /// </summary>
        internal bool IsUvXySwitched
        {
            get
            {
                return _isUvXySwitched;
            }

            set
            {
                if (value == _isUvXySwitched)
                    return;
                _isUvXySwitched = value;
                fireOnSettingsChanged();
            }
        }


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
        /// Explicit kerf for UV.
        /// </summary>
        internal double KerfUV
        {
            get
            {
                return _kerfUV;
            }

            set
            {
                if (value == _kerfUV)
                    return;

                _kerfUV = value;
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
                return _kerfXY;
            }

            set
            {
                if (value == _kerfXY)
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
        internal override IEnumerable<Point4Dstep> ItemPoints
        {
            get
            {
                return transformPoints(_shapeDefinition);
            }
        }

        internal ShapeItem(ReadableIdentifier name, IEnumerable<Point4Dmm> shapeDefinition)
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
            _shapeDefinition = (Point4Dmm[])info.GetValue("_shapeDefinition", typeof(Point4Dmm[]));
            _shapeMetricSize = (Size)info.GetValue("_shapeMetricSize", typeof(Size));
            _rotationAngle = info.GetDouble("_rotationAngle");
            _isUvXySwitched = info.GetBoolean("_isUvXySwitched");
            _useExplicitKerf = info.GetBoolean("_useExplicitKerf");
            _kerfUV = info.GetDouble("_kerfUV");
            _kerfXY = info.GetDouble("_kerfXY");

            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeDefinition", _shapeDefinition);
            info.AddValue("_shapeMetricSize", _shapeMetricSize);
            info.AddValue("_rotationAngle", _rotationAngle);
            info.AddValue("_isUvXySwitched", _isUvXySwitched);
            info.AddValue("_useExplicitKerf", _useExplicitKerf);
            info.AddValue("_kerfUV", _kerfUV);
            info.AddValue("_kerfXY", _kerfXY);
        }

        /// <summary>
        /// Sets size of the shape to original size (given by definition)
        /// </summary>
        internal virtual void SetOriginalSize()
        {
            var c1Diff = _shapeMaxC1 - _shapeMinC1;
            var c2Diff = _shapeMaxC2 - _shapeMinC2;
            if (c1Diff > c2Diff)
                MetricWidth = c1Diff;
            else
                MetricHeight = c2Diff;
        }


        /// <inheritdoc/>
        internal override void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin)
        {
            if (incommingJoin.Item2 != this)
                throw new NotSupportedException("Incomming join point is not valid.");

            var cuttingSpeed = workspace.CuttingSpeed;
            var cutPoints = CutPoints.ToArray();

            if (!cutPoints.First().Equals(cutPoints.Last()))
                throw new NotSupportedException("Shape is not closed.");

            //skip the repetitive point so we can join to whatever shape part.
            cutPoints = cutPoints.Take(cutPoints.Length - 1).ToArray();

            var outJoins = workspace.FindOutgoingJoins(this);
            var startIndex = incommingJoin.JoinPointIndex2;

            for (var i = startIndex + 1; i <= startIndex + cutPoints.Length; ++i)
            {
                var currentIndex = i % cutPoints.Length;
                var currentPoint = cutPoints[currentIndex];

                speedPoints.Add(currentPoint.With(cuttingSpeed));

                var currentOutgoingJoins = workspace.GetOutgoingJoinsFrom(currentIndex, outJoins);
                foreach (var currentOutgoingJoin in currentOutgoingJoins)
                {
                    currentOutgoingJoin.Build(workspace, speedPoints);
                }
            }
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

            var wSum = 0.0;
            for (var i = 1; i < _shapeDefinition.Length; ++i)
            {
                var x1 = _shapeDefinition[i - 1];
                var x2 = _shapeDefinition[i];

                wSum += (x2.U - x1.U) * (x2.V + x1.V);
            }
            _isClockwise = wSum > 0;
        }

        protected IEnumerable<Point4Dstep> transformPoints(IEnumerable<Point4Dmm> points)
        {
            var ratioC1 = 1.0 * _shapeMaxC1 - _shapeMinC1;
            if (ratioC1 == 0)
                ratioC1 = 1;

            var ratioC2 = 1.0 * _shapeMaxC2 - _shapeMinC2;
            if (ratioC2 == 0)
                ratioC2 = 1;

            foreach (var definitionPoint in points)
            {
                var point = rotate(definitionPoint);

                var u = (int)Math.Round((point.U - _shapeMinC1) / ratioC1 * _shapeMetricSize.Width / Constants.MilimetersPerStep);
                var v = (int)Math.Round((point.V - _shapeMinC2) / ratioC2 * _shapeMetricSize.Height / Constants.MilimetersPerStep);
                var x = (int)Math.Round((point.X - _shapeMinC1) / ratioC1 * _shapeMetricSize.Width / Constants.MilimetersPerStep);
                var y = (int)Math.Round((point.Y - _shapeMinC2) / ratioC2 * _shapeMetricSize.Height / Constants.MilimetersPerStep);

                if (_isUvXySwitched)
                {
                    //switch axes if neccessary
                    var uTemp = u;
                    var vTemp = v;
                    u = x;
                    v = y;
                    x = uTemp;
                    y = vTemp;
                }

                yield return new Point4Dstep(u + PositionC1, v + PositionC2, x + PositionC1, y + PositionC2);
            }
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
        protected PathFigure CreatePathFigure(IEnumerable<Point2Dstep> geometryPoints)
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
        protected Point4Dmm rotate(Point4Dmm point)
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
            return new Point4Dmm(
                rotatedU + c1, rotatedV + c2,
                rotatedX + c1, rotatedY + c2
                );
        }

        #region Kerf calculation

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

        protected IEnumerable<Point4Dmm> pointsWithKerf()
        {
            var workspace = Parent as WorkspacePanel;
            if (workspace == null || (workspace.CuttingKerf == 0.0 && !this.UseExplicitKerf))
                //there is no change
                return _shapeDefinition;


            var result = applyKerf(_shapeDefinition.Reverse(), workspace);
            return result;
        }

        protected Point4Dmm[] applyKerf(IEnumerable<Point4Dmm> points, WorkspacePanel workspace)
        {
            var pointsArr = points.ToArray();

            var result = new List<Point4Dmm>();
            for (var i = 0; i < pointsArr.Length; ++i)
            {
                var prevPoint = getNextDifferent(pointsArr, i, -1);
                var point = pointsArr[i];
                var nextPoint = getNextDifferent(pointsArr, i, 1);

                result.Add(applyKerf(prevPoint, point, nextPoint, workspace));
            }

            return result.ToArray();
        }

        private Point4Dmm getNextDifferent(Point4Dmm[] points, int startIndex, int increment)
        {
            var startPoint = points[startIndex];
            var startPointUV = startPoint.ToUV();
            var startPointXY = startPoint.ToXY();

            //find UV next
            var i = (startIndex + increment + points.Length) % points.Length;
            while (points[i].ToUV().Equals(startPointUV))
            {
                i = (i + increment + points.Length) % points.Length;
            }
            var endPointUV = points[i].ToUV();

            //find XY next
            i = (startIndex + increment + points.Length) % points.Length;
            while (points[i].ToXY().Equals(startPointXY))
            {
                i = (i + increment + points.Length) % points.Length;
            }
            var endPointXY = points[i].ToXY();

            return new Point4Dmm(endPointUV, endPointXY);
        }

        protected double reCalculateKerf(double kerf)
        {
            if (MetricWidth > MetricHeight)
            {
                var ratio = (_shapeMaxC1 - _shapeMinC1) / MetricWidth;
                kerf *= ratio;
            }
            else
            {
                var ratio = (_shapeMaxC2 - _shapeMinC2) / MetricHeight;
                kerf *= ratio;
            }

            if (!_isClockwise)
                kerf *= -1;

            return kerf;
        }

        #endregion
    }
}
