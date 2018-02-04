using ControllerCNC.Planning;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MillingRouter3D.GUI
{
    [Serializable]
    class EntryPoint : MillingItem
    {
        /// <summary>
        /// Size of displayed entry point.
        /// </summary>
        internal readonly static double EntryPointVisualDiameter = 20;

        internal EntryPoint()
            : base(new ReadableIdentifier("START"))
        {
            PositionX = 50;
            PositionY = 50;
            initialize();
        }

        internal EntryPoint(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            initialize();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            var entryPoint = new Ellipse();
            entryPoint.Width = EntryPointVisualDiameter;
            entryPoint.Height = EntryPointVisualDiameter;
            entryPoint.RenderTransform = new TranslateTransform(-EntryPointVisualDiameter / 2, -EntryPointVisualDiameter / 2);

            var brush = new SolidColorBrush(Colors.Green);
            brush.Opacity = 1.0;
            entryPoint.Fill = brush;

            return entryPoint;
        }

        protected override Point2Dmm getEntryPoint()
        {
            return new Point2Dmm(PositionX, PositionY);
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            foreach (var join in workspace.FindOutgoingJoins(this))
            {
                join.BuildPlan(builder, workspace);
            }
        }
    }
}
