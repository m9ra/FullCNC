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
    class ScaffoldItem : MillingWorkspaceItem
    {
        private Pen _scaffoldPen = new Pen();

        private readonly Point2Dmm[] _definition;

      
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
            var workspace = Parent as MillingWorkspaceItem;
            if (workspace == null)
                return;

            throw new NotImplementedException();
        }

       
        public ScaffoldItem ExtendBy(Point2Dmm point)
        {
            return new ScaffoldItem(Name, _definition.Concat(new[] { point }));
        }
    }
}
