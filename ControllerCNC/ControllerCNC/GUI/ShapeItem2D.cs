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
    class ShapeItem2D : ShapeItem
    {
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
        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get { return transformPoints(pointsWithKerf()); }
        }

        internal ShapeItem2D(ReadableIdentifier name, IEnumerable<Point2Dmm> shapeDefinition)
            : base(name, shapeDefinition.DuplicateTo4Dmm())
        {

        }

        internal ShapeItem2D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        internal override ShapeItem Clone(ReadableIdentifier cloneName)
        {
            var shapeItem = new ShapeItem2D(cloneName, ShapeDefinition.ToUV());
            shapeItem.MetricWidth = MetricWidth;
            shapeItem.RotationAngle = RotationAngle;
            shapeItem.MetricHeight = MetricHeight;
            return shapeItem;
        }

        protected override void constructionInitialization()
        {
            base.constructionInitialization();
            _itemBrush = new SolidColorBrush(Colors.LightGray);
            _itemBrush.Opacity = 0.4;

            _cutPen = new Pen(Brushes.Blue, 2.0);
            _cutPen.DashStyle = DashStyles.Dot;

            _itemPen = new Pen(Brushes.Black, 1.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var workspace = Parent as WorkspacePanel;
            if (workspace != null && workspace.CuttingKerf != 0.0)
            {
                var cutPoints = CutPoints;
                var cutUV = CreatePathFigure(cutPoints.ToUV());
                var cutGeometry = new PathGeometry(new[] { cutUV });
                drawingContext.DrawGeometry(null, _cutPen, cutGeometry);
            }

            var figure = CreatePathFigure(ItemPoints.ToUV());
            var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);
        }

        private IEnumerable<Point4Dmm> pointsWithKerf()
        {
            var workspace = Parent as WorkspacePanel;
            if (workspace == null || workspace.CuttingKerf == 0.0)
                //there is no change
                return _shapeDefinition;

            var kerf = workspace.CuttingKerf;
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

            var resultUV = applyKerf(_shapeDefinition.Reverse().ToUV(), kerf);
            var resultXY = applyKerf(_shapeDefinition.Reverse().ToXY(), kerf);

            var result = new List<Point4Dmm>();
            for (var i = 0; i < _shapeDefinition.Length; ++i)
            {
                var point = new Point4Dmm(resultUV[i], resultXY[i]);
                result.Add(point);
            }

            return result;
        }

        private Point2Dmm[] applyKerf(IEnumerable<Point2Dmm> points, double kerf)
        {
            var pointsArr = points.ToArray();

            var result = new List<Point2Dmm>();
            for (var i = 0; i < pointsArr.Length; ++i)
            {
                var prevPoint = getNextDifferent(pointsArr, i, -1);
                var point = pointsArr[i];
                var nextPoint = getNextDifferent(pointsArr, i, 1);

                result.Add(applyKerf(prevPoint, point, nextPoint, kerf));
            }

            return result.ToArray();
        }

        private Point2Dmm getNextDifferent(Point2Dmm[] points, int startIndex, int increment)
        {
            var startPoint = points[startIndex];
            var i = (startIndex + increment + points.Length) % points.Length;
            while (points[i].Equals(startPoint))
            {
                i = (i + increment + points.Length) % points.Length;
            }

            return points[i];
        }

        private Point2Dmm applyKerf(Point2Dmm p1, Point2Dmm p2, Point2Dmm p3, double kerf)
        {
            var diff12_C1 = p2.C1 - p1.C1;
            var diff12_C2 = p2.C2 - p1.C2;

            var diff23_C1 = p3.C1 - p2.C1;
            var diff23_C2 = p3.C2 - p2.C2;

            var v1 = new Vector(diff12_C1, diff12_C2);
            var v2 = new Vector(diff23_C1, diff23_C2);

            var nV1 = new Vector(v1.Y, -v1.X);
            var nV2 = new Vector(v2.Y, -v2.X);

            nV1.Normalize();
            nV2.Normalize();

            var shift = (nV1 + nV2) * kerf / 2;
            return new Point2Dmm(p2.C1 + shift.X, p2.C2 + shift.Y);
        }
    }
}
