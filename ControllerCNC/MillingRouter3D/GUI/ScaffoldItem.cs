using ControllerCNC.Planning;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MillingRouter3D.GUI
{
    [Serializable]
    class ScaffoldItem : PermanentMillingWorkspaceItem
    {
        private Pen _scaffoldPen = new Pen();

        private readonly Point2Dmm[] _definition;

        internal IEnumerable<Point2Dmm> ShapePoints => _definition.Select(p => new Point2Dmm(p.C1 + PositionX, p.C2 + PositionY)).ToArray();

        internal ScaffoldItem(ReadableIdentifier name, IEnumerable<Point2Dmm> points)
            : base(name)
        {
            _definition = points.ToArray();

            constructionInitialization();
        }

        internal ScaffoldItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _definition = (Point2Dmm[])info.GetValue("_definition", typeof(Point2Dmm[]));

            constructionInitialization();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("_definition", _definition);
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            //the rendering is controlled directly by current object
            return null;
        }

        /// <inheritdoc/>
        protected void constructionInitialization()
        {
            _scaffoldPen = new Pen(Brushes.Violet, 5.0);
            _scaffoldPen.DashStyle = DashStyles.Dot;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var workspace = Parent as MillingWorkspacePanel;
            if (workspace == null)
                return;

            var transformedPoints = ShapePoints.ToArray();

            var figure = CreatePathFigure(transformedPoints);
            var cutGeometry = new PathGeometry(new[] { figure });
            drawingContext.DrawGeometry(null, _scaffoldPen, cutGeometry);

            var radius = 10;
            foreach (var point in transformedPoints)
            {
                var visualPoint = ConvertToVisual(point);
                drawingContext.DrawEllipse(_scaffoldPen.Brush, null, visualPoint, radius, radius);
            }
        }

        public ScaffoldItem ExtendBy(Point2Dmm point)
        {
            var item = new ScaffoldItem(Name, ShapePoints.Concat(new[] { point }));
            return item;
        }
    }
}
