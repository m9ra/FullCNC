using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ControllerCNC.GUI
{
    class HeadCNC : WorkspaceItem
    {
        private readonly Color _fillColor;

        private readonly bool _isTopDown;

        internal HeadCNC(Color fillColor, bool isTopDown)
        {
            _fillColor = fillColor;
            _isTopDown = isTopDown;

            initialize();
        }

        /// <inheritdoc/>
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            var polygon = new Polygon();

            var yCoord = 60;
            var xCoord = _isTopDown ? -20 : 20;
            polygon.Points.Add(new Point(0, yCoord));
            polygon.Points.Add(new Point(xCoord, yCoord));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(xCoord, -yCoord));
            polygon.Points.Add(new Point(0, -yCoord));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(yCoord, xCoord));
            polygon.Points.Add(new Point(yCoord, 0));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(-yCoord, xCoord));
            polygon.Points.Add(new Point(-yCoord, 0));
            polygon.Points.Add(new Point(0, 0));

            var fillBrush = new SolidColorBrush(_fillColor);
            fillBrush.Opacity = 0.3;
            polygon.Fill = fillBrush;
            return polygon;
        }
    }
}
