using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using System.Runtime.Serialization;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    [Serializable]
    class EntryPoint : PointProviderItem
    {
        /// <summary>
        /// Size of displayed entry point.
        /// </summary>
        internal readonly static double EntryPointVisualDiameter = 20;

        /// <inheritdoc/>
        internal override IEnumerable<Point4D> ItemPoints
        {
            get { return new[] { new Point4D(PositionX, PositionY, PositionX, PositionY) }; }
        }

        internal EntryPoint()
        {
            initialize();
        }

        internal EntryPoint(SerializationInfo info, StreamingContext context) : base(info, context) {
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
            brush.Opacity = 0.3;
            entryPoint.Fill = brush;

            return entryPoint;
        }
    }
}
