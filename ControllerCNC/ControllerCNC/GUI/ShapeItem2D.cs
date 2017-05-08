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
            get { return translateToWorkspace(TransformedShapeDefinitionWithKerf); }
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

            var itemPoints = translateToWorkspace(TransformedShapeDefinition);
            var figure = CreatePathFigure(itemPoints.ToUV());
            var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);
        }

        protected override Point4Dmm applyKerf(Point4Dmm p1, Point4Dmm p2, Point4Dmm p3, WorkspacePanel workspace)
        {
            var kerf = reCalculateKerf(workspace.CuttingKerf);
            var shift = calculateKerfShift(p1.ToUV(), p2.ToUV(), p3.ToUV(), kerf);
            return new Point4Dmm(p2.U + shift.X, p2.V + shift.Y, p2.X + shift.X, p2.Y + shift.Y);
        }
    }
}
