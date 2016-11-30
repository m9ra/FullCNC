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

        /// <inheritdoc/>
        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get { return ItemPoints; }
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

            _itemPen = new Pen(Brushes.Black, 1.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var figure = CreatePathFigure(ItemPoints.ToUV());
            var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);
        }
    }
}
