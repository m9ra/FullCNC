using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ControllerCNC.Primitives;

using System.Runtime.Serialization;
using System.Windows;

namespace ControllerCNC.GUI
{
    [Serializable]
    class ScaffoldItem : PointProviderItem
    {
        private Pen _scaffoldPen = new Pen();

        private readonly Point2Dstep[] _definition;

        /// <inheritdoc/>
        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get
            {
                return _definition.Select(p => new Point4Dstep(p.C1 + PositionC1, p.C2 + PositionC2, p.C1 + PositionC1, p.C2 + PositionC2)).ToArray();
            }
        }

        internal ScaffoldItem(ReadableIdentifier name, IEnumerable<Point2Dstep> points)
            : base(name)
        {
            _definition = points.ToArray();

            constructionInitialization();
        }

        internal ScaffoldItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _definition = (Point2Dstep[])info.GetValue("_definition", typeof(Point2Dstep[]));

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
            var workspace = Parent as WorkspacePanel;
            if (workspace == null)
                return;

            var cutPoints = CutPoints.ToUV().ToArray();
            var cutUV = CreatePathFigure(cutPoints);
            var cutGeometry = new PathGeometry(new[] { cutUV });
            drawingContext.DrawGeometry(null, _scaffoldPen, cutGeometry);

            var radius = 10;
            foreach (var point in cutPoints)
            {
                var visualPoint = ConvertToVisual(point);
                drawingContext.DrawEllipse(_scaffoldPen.Brush, null, visualPoint, radius, radius);
            }
        }

        /// <inheritdoc/>
        internal override void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin)
        {
            //there is nothing to build
        }

        internal ScaffoldItem ExtendBy(Point2Dstep point)
        {
            return new ScaffoldItem(Name, _definition.Concat(new[] { point }));
        }
    }
}
