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
                var kerfPoints = transformPoints(pointsWithKerf()).ToArray();
                //kerfPoints = ItemPoints.ToArray();
                return new PlaneProjector(_shapeMetricThickness, _wireLength).Project(kerfPoints);
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
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_shapeMetricThickness", _shapeMetricThickness);
        }

        /// <inheritdoc/>
        internal override ShapeItem Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new ShapeItem4D(cloneName, ShapeDefinition);
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            shapeItem.MetricThickness = MetricThickness;
            return shapeItem;
        }

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
            var points = ItemPoints;
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
            getShapeSpeedVectors(workspace, p1, p2, out Vector speedVector12UV, out Vector speedVector12XY);
            getShapeSpeedVectors(workspace, p2, p3, out Vector speedVector23UV, out Vector speedVector23XY);

            var speedUV = (speedVector12UV.Length + speedVector23UV.Length) / 2;
            var speedXY = (speedVector12XY.Length + speedVector23XY.Length) / 2;

            var referentialKerf = workspace.CuttingKerf;
            var kerfUV = reCalculateKerf(referentialKerf, speedUV, workspace);
            var kerfXY = reCalculateKerf(referentialKerf, speedXY, workspace);

            var shiftUV = calculateKerfShift(p1.ToUV(), p2.ToUV(), p3.ToUV(), kerfUV);
            var shiftXY = calculateKerfShift(p1.ToXY(), p2.ToXY(), p3.ToXY(), kerfXY);

            return new Point4Dmm(p2.U + shiftUV.X, p2.V + shiftUV.Y, p2.X + shiftXY.X, p2.Y + shiftXY.Y);
        }

        double reCalculateKerf(double referentialKerf, double metricSpeed, WorkspacePanel workspace)
        {
            referentialKerf = reCalculateKerf(referentialKerf);
            var referentialSpeed = workspace.CuttingSpeed;
            var metricReferentialSpeed = Constants.MilimetersPerStep * referentialSpeed.StepCount / (1.0 * referentialSpeed.Ticks / Constants.TimerFrequency);

            var referenceFactor = metricSpeed / metricReferentialSpeed;

            return referentialKerf * referenceFactor;
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
