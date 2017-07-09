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
    enum SpeedAlgorithm { TowerBased, StickToFacetUV, StickToFacetXY };

    [Serializable]
    class ShapeItem4D : ShapeItem
    {
        /// <summary>
        /// Thickness (distance between shape facets) in mm.
        /// </summary>
        private double _shapeMetricThickness;

        /// <summary>
        /// Actual length of the wire.
        /// </summary>
        private double _wireLength;

        /// <summary>
        /// Brush for the first facet fill.
        /// </summary>
        private Brush _itemBrushUV;

        /// <summary>
        /// Brush for the second facet fill.
        /// </summary>
        private Brush _itemBrushXY;

        /// <summary>
        /// Pen for item border.
        /// </summary>
        private Pen _itemPen;

        /// <summary>
        /// Pen for cut path of first facet.
        /// </summary>
        private Pen _cutPenUV;

        /// <summary>
        /// Pen for cut path of second facet.
        /// </summary>
        private Pen _cutPenXY;

        /// <summary>
        /// Algorithm for cutting speed computation.
        /// </summary>
        private SpeedAlgorithm _speedAlgorithm;

        /// <summary>
        /// Determine whether templated cut will be used.
        /// </summary>
        private bool _useTemplatedCut = false;

        /// <summary>
        /// If positive - margin around top of the
        /// </summary>
        private double _finishCutMetricMarginC2 = 0.0;

        internal double FinishCutMetricMarginC2
        {
            get
            {
                return _finishCutMetricMarginC2;
            }

            set
            {
                if (value == _finishCutMetricMarginC2)
                    return;

                _finishCutMetricMarginC2 = value;
                fireOnSettingsChanged();
            }
        }

        internal SpeedAlgorithm SpeedAlgorithm
        {
            get
            {
                return _speedAlgorithm;
            }
            set
            {
                if (value == _speedAlgorithm)
                    return;

                _speedAlgorithm = value;
                fireOnSettingsChanged();
            }
        }

        /// <summary>
        /// Thickness (distance between shape facets) in mm.
        /// </summary>
        internal double MetricThickness
        {
            get
            {
                return _shapeMetricThickness;
            }

            set
            {
                if (value == _shapeMetricThickness)
                    return;

                _shapeMetricThickness = value;
                fireOnSettingsChanged();
            }
        }

        /// <inheritdoc/>
        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get
            {
                var projectedPoints = new PlaneProjector(_shapeMetricThickness, _wireLength).Project(CutDefinition);
                return translateToWorkspace(projectedPoints);
            }
        }

        internal IEnumerable<Point4Dmm> CutDefinition
        {
            get
            {
                IEnumerable<Point4Dmm> shapePoints;
                if (_useTemplatedCut)
                    shapePoints = templatedCutPoints().ToArray();
                else if (_finishCutMetricMarginC2 > 0)
                    shapePoints = cutPointsWithWidthFinish().ToArray();
                else
                    shapePoints = TransformedShapeDefinitionWithKerf.ToArray();

                return shapePoints.ToArray();
            }
        }

        /// <inheritdoc/>
        internal override bool AllowFlexibleEntrance
        {
            get
            {
                return !_useTemplatedCut && !(_finishCutMetricMarginC2 > 0);
            }
        }

        internal ShapeItem4D(ReadableIdentifier name, IEnumerable<Point4Dmm> shapeDefinition)
            : base(name, shapeDefinition)
        {
        }

        internal ShapeItem4D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shapeMetricThickness = (double)info.GetValue("_shapeMetricThickness", typeof(double));
            _speedAlgorithm = (SpeedAlgorithm)info.GetValue("_speedAlgorithm", typeof(SpeedAlgorithm));
            _finishCutMetricMarginC2 = (double)info.GetValue("_finishCutMetricMarginC2", typeof(double));
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeMetricThickness", _shapeMetricThickness);
            info.AddValue("_speedAlgorithm", _speedAlgorithm);
            info.AddValue("_finishCutMetricMarginC2", _finishCutMetricMarginC2);
        }

        /// <inheritdoc/>
        internal override ShapeItem Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new ShapeItem4D(cloneName, ShapeDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            shapeItem.MetricThickness = MetricThickness;
            shapeItem.UseClockwiseCut = UseClockwiseCut;
            shapeItem.UseExplicitKerf = UseExplicitKerf;
            shapeItem.KerfUV = KerfUV;
            shapeItem.KerfXY = KerfXY;
            shapeItem.IsUvXySwitched = IsUvXySwitched;
            shapeItem.SpeedAlgorithm = SpeedAlgorithm;
            shapeItem.FinishCutMetricMarginC2 = FinishCutMetricMarginC2;

            return shapeItem;
        }

        /// <inheritdoc/>
        internal override void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin)
        {
            if (_speedAlgorithm == SpeedAlgorithm.TowerBased || !UseExplicitKerf)
            {
                base.Build(workspace, speedPoints, incommingJoin);
                return;
            }

            if (incommingJoin.Item2 != this)
                throw new NotSupportedException("Incomming join point is not valid.");

            var cuttingSpeed = workspace.CuttingSpeed;
            var cutPoints = CutPoints.ToArray();

            if (!cutPoints.First().Equals(cutPoints.Last()))
                throw new NotSupportedException("Shape is not closed.");

            var definitionPoints = CutDefinition.ToArray();
            if (cutPoints.Count() != definitionPoints.Count())
                throw new NotSupportedException("Invalid cut points count.");

            //skip the repetitive point so we can join to whatever shape part.
            cutPoints = cutPoints.Take(cutPoints.Length - 1).ToArray();
            definitionPoints.Take(definitionPoints.Length - 1).ToArray();

            var projector = new PlaneProjector(_shapeMetricThickness, _wireLength);

            var outJoins = workspace.FindOutgoingJoins(this);
            var startIndex = incommingJoin.JoinPointIndex2;

            for (var i = startIndex + 1; i <= startIndex + cutPoints.Length; ++i)
            {
                var currentIndex = i % cutPoints.Length;
                var currentPoint = cutPoints[currentIndex];

                var speeds = getSpeeds(definitionPoints, currentIndex, cuttingSpeed, projector);
                //System.Diagnostics.Debug.Print(speeds.ToString());
                var speed1Limit = speeds.Item1.ToDeltaT() >= Constants.StartDeltaT || speeds.Item1.ToDeltaT() < 0;
                var speed2Limit = speeds.Item2.ToDeltaT() >= Constants.StartDeltaT || speeds.Item2.ToDeltaT() < 0;

                if (!speed1Limit || !speed2Limit)
                    throw new PlanningException("Speed limit exceeded");

                speedPoints.Add(currentPoint.With(speeds.Item1, speeds.Item2));

                var currentOutgoingJoins = workspace.GetOutgoingJoinsFrom(currentIndex, outJoins);
                foreach (var currentOutgoingJoin in currentOutgoingJoins)
                {
                    currentOutgoingJoin.Build(workspace, speedPoints);
                }
            }
        }

        private IEnumerable<Point4Dmm> addHorizontalKerf(IEnumerable<Point4Dmm> points)
        {
            var originalPoints = points.ToArray();
            var kerfPoints = addKerf(originalPoints).ToArray();

            var result = new List<Point4Dmm>();
            for (var i = 0; i < kerfPoints.Length; ++i)
            {
                var kerfPoint = kerfPoints[i];
                var originalPoint = originalPoints[i];
                result.Add(new Point4Dmm(kerfPoint.U, originalPoint.V, kerfPoint.X, originalPoint.Y));
            }

            return result;
        }

        private IEnumerable<Point4Dmm> cutPointsWithWidthFinish()
        {
            //find top/bottom templates
            var definitionPoints = TransformedShapeDefinitionWithKerf.ToArray();

            double minHorizontalCoord, maxHorizontalCoord, minVerticalCoord;
            int minHorizontalIndex, maxHorizontalIndex, minVerticalIndex;
            findBoxPoints(definitionPoints, out minHorizontalCoord, out maxHorizontalCoord, out minVerticalCoord, out minHorizontalIndex, out maxHorizontalIndex, out minVerticalIndex);

            var result = new List<Point4Dmm>();

            for (var i = minHorizontalIndex; i < minHorizontalIndex + definitionPoints.Length; ++i)
            {
                //unwind shape in the desired form
                result.Add(definitionPoints[i % definitionPoints.Length]);
            }

            var belowCutMargin = 5;
            var syncMarginC1 = -5;
            var marginC2 = -_finishCutMetricMarginC2;

            var entryPoint = result.First();
            var aboveEntryPoint = new Point4Dmm(entryPoint.U, entryPoint.V + marginC2, entryPoint.X, entryPoint.Y + marginC2);
            var belowEntryPoint = new Point4Dmm(entryPoint.U, entryPoint.V + belowCutMargin, entryPoint.X, entryPoint.Y + belowCutMargin);
            var syncEntryPoint = new Point4Dmm(entryPoint.U + syncMarginC1, entryPoint.V, entryPoint.X + syncMarginC1, entryPoint.Y);
            var aboveSyncEntryPoint = new Point4Dmm(syncEntryPoint.U, syncEntryPoint.V + marginC2, syncEntryPoint.X, syncEntryPoint.Y + marginC2);

            result.Insert(0, syncEntryPoint);
            result.Add(syncEntryPoint);
            result.Add(aboveSyncEntryPoint);
            result.Add(aboveEntryPoint);
            result.Add(belowEntryPoint);
            result.Add(syncEntryPoint);

            return result;
        }

        private IEnumerable<Point4Dmm> templatedCutPoints()
        {
            //find top/bottom templates
            var definitionPoints = TransformedShapeDefinition.ToArray();
            definitionPoints = addHorizontalKerf(definitionPoints).ToArray();

            double minHorizontalCoord, maxHorizontalCoord, minVerticalCoord;
            int minHorizontalIndex, maxHorizontalIndex, minVerticalIndex;
            findBoxPoints(definitionPoints, out minHorizontalCoord, out maxHorizontalCoord, out minVerticalCoord, out minHorizontalIndex, out maxHorizontalIndex, out minVerticalIndex);

            var templateMinMax = new List<Point4Dmm>();
            var templateMaxMin = new List<Point4Dmm>();
            var isMinMax = true;

            for (var i = minHorizontalIndex; i < minHorizontalIndex + definitionPoints.Length; ++i)
            {
                var pointIndex = i % definitionPoints.Length;
                var point = definitionPoints[pointIndex];

                if (isMinMax)
                    //first template points
                    templateMinMax.Add(point);

                if (pointIndex == maxHorizontalIndex)
                    isMinMax = false;

                if (!isMinMax)
                    //second template points
                    templateMaxMin.Add(point);
            }

            //distinguish which template is top, and which bottom
            var isMinMaxTop = minHorizontalIndex > minVerticalIndex && minVerticalIndex < maxHorizontalIndex;
            if (minHorizontalIndex == minVerticalIndex || maxHorizontalIndex == minVerticalIndex)
                throw new NotImplementedException("Degenerated shape, probably not good for templated cut");


            templateMaxMin.Reverse();// the template was drawn in the opposite direction

            var topTemplate = isMinMaxTop ? templateMinMax : templateMaxMin;
            var bottomTemplate = isMinMaxTop ? templateMaxMin : templateMinMax;

            //define template scaffold points
            var startPoint = topTemplate.First();
            var endPoint = topTemplate.Last();

            var margin = 30;
            var marginTop = 55;
            var syncMargin = 5;
            var topLineC2 = minVerticalCoord - marginTop; //TODO load from shape settings
            var entryC1 = minHorizontalCoord - margin; //TODO load from shape settings
            var finishC1 = maxHorizontalCoord + margin; //TODO load from shape settings

            var entryScaffoldPoint = new Point4Dmm(entryC1, startPoint.V, entryC1, startPoint.Y);
            var syncEntryScaffoldPoint = new Point4Dmm(startPoint.U - syncMargin, startPoint.V, startPoint.X - syncMargin, startPoint.Y);
            var aboveEntryScaffoldPoint = new Point4Dmm(entryC1, topLineC2, entryC1, topLineC2);

            var finishScaffoldPoint = new Point4Dmm(finishC1, endPoint.V, finishC1, endPoint.Y);
            var syncFinishScaffoldPoint = new Point4Dmm(endPoint.U + syncMargin, endPoint.V, endPoint.X + syncMargin, endPoint.Y);
            var aboveFinishScaffoldPoint = new Point4Dmm(finishC1, topLineC2, finishC1, topLineC2);

            //construct the cut
            var points = new List<Point4Dmm>();

            //bottom template first
            points.Add(entryScaffoldPoint);
            points.Add(syncEntryScaffoldPoint);
            points.AddRange(bottomTemplate);
            points.Add(syncFinishScaffoldPoint);
            points.Add(finishScaffoldPoint);
            points.Add(aboveFinishScaffoldPoint);
            points.Add(aboveEntryScaffoldPoint);
            points.Add(entryScaffoldPoint);
            points.Add(syncEntryScaffoldPoint);

            //top template cut
            points.AddRange(topTemplate);
            points.Add(syncFinishScaffoldPoint);
            points.Add(finishScaffoldPoint);
            points.Add(aboveFinishScaffoldPoint);
            points.Add(aboveEntryScaffoldPoint);
            points.Add(entryScaffoldPoint);

            //entry point at the end is implicit (shape is closed)
            return points;
        }

        private static void findBoxPoints(Point4Dmm[] definitionPoints, out double minHorizontalCoord, out double maxHorizontalCoord, out double minVerticalCoord, out int minHorizontalIndex, out int maxHorizontalIndex, out int minVerticalIndex)
        {
            minHorizontalCoord = double.PositiveInfinity;
            maxHorizontalCoord = double.NegativeInfinity;
            minVerticalCoord = double.PositiveInfinity;
            minHorizontalIndex = -1;
            maxHorizontalIndex = -1;
            minVerticalIndex = -1;
            for (var i = 0; i < definitionPoints.Length; ++i)
            {
                var point = definitionPoints[i];

                var currMaxHorizontal = Math.Max(point.U, point.X);
                var currMinHorizontal = Math.Min(point.U, point.X);
                var currMinVertical = Math.Min(point.V, point.Y);

                if (currMaxHorizontal > maxHorizontalCoord)
                {
                    maxHorizontalIndex = i;
                    maxHorizontalCoord = currMaxHorizontal;
                }

                if (currMinHorizontal < minHorizontalCoord)
                {
                    minHorizontalIndex = i;
                    minHorizontalCoord = currMinHorizontal;
                }

                if (currMinVertical < minVerticalCoord)
                {
                    minVerticalIndex = i;
                    minVerticalCoord = currMinVertical;
                }
            }
        }

        private Tuple<Speed, Speed> getSpeeds(Point4Dmm[] points, int currentIndex, Speed facetSpeed, PlaneProjector projector)
        {
            var currentPoint = points[currentIndex % points.Length];
            var nextPoint = points[(currentIndex + 1) % points.Length];

            getFacetVectors(currentPoint, nextPoint, out var facetUV, out var facetXY);

            var facetSpeedConverted = 1.0 * facetSpeed.StepCount / facetSpeed.Ticks;

            if (facetUV.Length <= Constants.MilimetersPerStep && facetXY.Length <= Constants.MilimetersPerStep)
                //TODO  this accounts for numerical instability
                return Tuple.Create(facetSpeed, facetSpeed);

            double ratio;
            switch (SpeedAlgorithm)
            {
                case SpeedAlgorithm.StickToFacetUV:
                    ratio = facetSpeedConverted / facetUV.Length;
                    break;
                case SpeedAlgorithm.StickToFacetXY:
                    ratio = facetSpeedConverted / facetXY.Length;
                    break;
                default:
                    throw new NotImplementedException("SpeedAlgorithm");
            }

            facetUV = facetUV * ratio;
            facetXY = facetXY * ratio;

            var speedPoint = new Point4Dmm(currentPoint.U + facetUV.X, currentPoint.V + facetUV.Y, currentPoint.X + facetXY.X, currentPoint.Y + facetXY.Y);

            var currentProjected = projector.Project(currentPoint);
            var speedProjected = projector.Project(speedPoint);

            getFacetVectors(currentProjected, speedProjected, out var speedUV, out var speedXY);

            var speedFactor = Constants.TimerFrequency;

            var uvSpeed = speedUV.Length * speedFactor;
            var xySpeed = speedXY.Length * speedFactor;

            return Tuple.Create(
                new Speed((long)(uvSpeed), speedFactor),
                new Speed((long)(xySpeed), speedFactor)
                );
        }

        private void getFacetVectors(Point4Dmm p1, Point4Dmm p2, out Vector v1, out Vector v2)
        {
            v1 = new Vector(p2.U - p1.U, p2.V - p1.V);
            v2 = new Vector(p2.X - p1.X, p2.Y - p1.Y);
        }

        /// <inheritdoc/>
        internal override void RecalculateToWorkspace(WorkspacePanel workspace, Size size)
        {
            base.RecalculateToWorkspace(workspace, size);
            _wireLength = workspace.WireLength;
        }

        /// <inheritdoc/>
        protected override void constructionInitialization()
        {
            base.constructionInitialization();

            _itemBrushUV = new SolidColorBrush(Colors.Green);
            _itemBrushXY = new SolidColorBrush(Colors.LightBlue);
            _itemBrushUV.Opacity = _itemBrushXY.Opacity = 0.4;

            _cutPenUV = new Pen(Brushes.Blue, 2.0);
            _cutPenXY = new Pen(Brushes.Red, 2.0);
            _cutPenUV.DashStyle = DashStyles.Dot;
            _cutPenXY.DashStyle = DashStyles.Dot;

            _itemPen = new Pen(Brushes.Black, 1.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var points = translateToWorkspace(TransformedShapeDefinition);
            var figureUV = CreatePathFigure(points.ToUV());
            var figureXY = CreatePathFigure(points.ToXY());

            var cutPoints = CutPoints.ToArray();
            var cutUV = CreatePathFigure(cutPoints.ToUV());
            var cutXY = CreatePathFigure(cutPoints.ToXY());

            var geometryUV = new PathGeometry(new[] { figureUV }, FillRule.EvenOdd, Transform.Identity);
            var geometryXY = new PathGeometry(new[] { figureXY }, FillRule.EvenOdd, Transform.Identity);
            var geometryCutUV = new PathGeometry(new[] { cutUV });
            var geometryCutXY = new PathGeometry(new[] { cutXY });

            drawingContext.DrawGeometry(_itemBrushUV, _itemPen, geometryUV);
            drawingContext.DrawGeometry(_itemBrushXY, _itemPen, geometryXY);
            drawingContext.DrawGeometry(null, _cutPenUV, geometryCutUV);
            drawingContext.DrawGeometry(null, _cutPenXY, geometryCutXY);
        }

        /// <inheritdoc/>
        protected override Point4Dmm applyKerf(Point4Dmm p1, Point4Dmm p2, Point4Dmm p3, WorkspacePanel workspace)
        {
            double kerfUV, kerfXY;

            if (UseExplicitKerf)
            {
                kerfUV = reCalculateKerf(KerfUV);
                kerfXY = reCalculateKerf(KerfXY);
            }
            else
            {
                getShapeSpeedVectors(workspace, p1, p2, out Vector speedVector12UV, out Vector speedVector12XY);
                getShapeSpeedVectors(workspace, p2, p3, out Vector speedVector23UV, out Vector speedVector23XY);

                var speedUV = (speedVector12UV.Length + speedVector23UV.Length) / 2;
                var speedXY = (speedVector12XY.Length + speedVector23XY.Length) / 2;

                var referentialKerf = workspace.CuttingKerf;
                kerfUV = reCalculateKerf(referentialKerf, speedUV, workspace);
                kerfXY = reCalculateKerf(referentialKerf, speedXY, workspace);
            }
            var shiftUV = calculateKerfShift(p1.ToUV(), p2.ToUV(), p3.ToUV(), kerfUV);
            var shiftXY = calculateKerfShift(p1.ToXY(), p2.ToXY(), p3.ToXY(), kerfXY);

            return new Point4Dmm(p2.U + shiftUV.X, p2.V + shiftUV.Y, p2.X + shiftXY.X, p2.Y + shiftXY.Y);
        }

        private double reCalculateKerf(double referentialKerf, double metricSpeed, WorkspacePanel workspace)
        {
            referentialKerf = reCalculateKerf(referentialKerf);
            var referentialSpeed = workspace.CuttingSpeed;
            var metricReferentialSpeed = Constants.MilimetersPerStep * referentialSpeed.StepCount / (1.0 * referentialSpeed.Ticks / Constants.TimerFrequency);

            var referenceFactor = metricReferentialSpeed / metricSpeed;

            var wireKerf = Math.Min(Constants.HotwireThickness / 2, Math.Abs(referentialKerf));
            var radiationKerf = Math.Abs(referentialKerf) - wireKerf;
            var adjustedKerf = radiationKerf * referenceFactor + wireKerf;

            return Math.Sign(referentialKerf) * adjustedKerf;
        }

        private void getShapeSpeedVectors(WorkspacePanel workspace, Point4Dmm p1, Point4Dmm p2, out Vector speedVector12UV, out Vector speedVector12XY)
        {
            var wireLength = workspace.WireLength;

            var t1 = projectToTowers(p1, wireLength);
            var t2 = projectToTowers(p2, wireLength);

            //tower speeds
            getSpeedVectors(workspace, t1, t2, out Vector speedVector12UVt, out Vector speedVector12XYt);

            var facetDistance = wireLength / 2 - MetricThickness / 2;
            var facetRatio = 1.0 - facetDistance / wireLength;
            speedVector12UV = speedVector12UVt * facetRatio + speedVector12XYt * (1.0 - facetRatio);
            speedVector12XY = speedVector12XYt * facetRatio + speedVector12UVt * (1.0 - facetRatio);
        }

        private void getSpeedVectors(WorkspacePanel workspace, Point4Dmm t1, Point4Dmm t2, out Vector speedVector12UVt, out Vector speedVector12XYt)
        {
            var maxSpeed = workspace.CuttingSpeed;
            var maxSpeedRatio = (maxSpeed.StepCount * Constants.MilimetersPerStep) / (1.0 * maxSpeed.Ticks / Constants.TimerFrequency);

            //tower speeds
            speedVector12UVt = diffVector(t1.ToUV(), t2.ToUV());
            speedVector12XYt = diffVector(t1.ToXY(), t2.ToXY());
            if (speedVector12UVt.Length > speedVector12XYt.Length)
            {
                var speedRatio = speedVector12XYt.Length / speedVector12UVt.Length;
                speedVector12UVt.Normalize();
                speedVector12XYt.Normalize();

                speedVector12UVt = speedVector12UVt * maxSpeedRatio;
                speedVector12XYt = speedVector12XYt * speedRatio;
            }
            else
            {
                var speedRatio = speedVector12UVt.Length / speedVector12XYt.Length;
                speedVector12UVt.Normalize();
                speedVector12XYt.Normalize();

                speedVector12UVt = speedVector12UVt * maxSpeedRatio;
                speedVector12XYt = speedVector12XYt * speedRatio;
            }
        }

        private Point4Dmm projectToTowers(Point4Dmm p, double wireLength)
        {
            return PlaneProjector.Project(p, this.MetricThickness, wireLength);
        }
    }
}
