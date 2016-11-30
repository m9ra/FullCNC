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
            get { return new PlaneProjector(_shapeMetricThickness).Project(ItemPoints); }
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

            var cutPoints = CutPoints;
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
    }
}
