using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerCNC.Primitives;
using System.Runtime.Serialization;
using System.Windows.Media;

namespace ControllerCNC.GUI
{
    [Serializable]
    class NativeControlItem : ShapeItem
    {

        /// <summary>
        /// Pen for item border.
        /// </summary>
        private Pen _itemPen;

        /// <summary>
        /// Speeds for segments.
        /// </summary>
        internal IEnumerable<Speed> SegmentSpeeds { get; private set; }

        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get
            {
                return ItemPoints;
            }
        }

        internal NativeControlItem(ReadableIdentifier name, IEnumerable<Point2Dmm> shapeDefinition, IEnumerable<Speed> segmentSpeeds)
            : base(name, shapeDefinition.DuplicateTo4Dmm())
        {
            SegmentSpeeds = segmentSpeeds;
        }

        internal NativeControlItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            SegmentSpeeds = (IEnumerable<Speed>)info.GetValue("SegmentSpeeds", typeof(IEnumerable<Speed>));
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("SegmentSpeeds", SegmentSpeeds);
        }

        /// <inheritdoc/>
        internal override ShapeItem Clone(ReadableIdentifier cloneName)
        {
            return new NativeControlItem(cloneName, ShapeDefinition.ToUV(), SegmentSpeeds);
        }

        /// <inheritdoc/>
        protected override void constructionInitialization()
        {
            base.constructionInitialization();
            _itemPen = new Pen(Brushes.Black, 10.0);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var figure = CreatePathFigure(ItemPoints.ToUV());
            var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
            drawingContext.DrawGeometry(null, _itemPen, geometry);
        }

        /// <inheritdoc/>
        protected override Point4Dmm applyKerf(Point4Dmm p1, Point4Dmm p2, Point4Dmm p3, WorkspacePanel workspace)
        {
            // Native item does not follow kerf settup.
            return p2;
        }
    }
}
