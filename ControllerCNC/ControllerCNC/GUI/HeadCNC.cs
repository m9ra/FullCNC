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
        protected override object createContent()
        {
            var polygon = new Polygon();

            var yCoord = _isTopDown ? -60 : 60;
            polygon.Points.Add(new Point(-10, yCoord));
            polygon.Points.Add(new Point(10, yCoord));
            polygon.Points.Add(new Point(0, 0));

            var fillBrush = new SolidColorBrush(_fillColor);
            fillBrush.Opacity = 0.3;
            polygon.Fill = fillBrush;
            return polygon;
        }
    }
}
