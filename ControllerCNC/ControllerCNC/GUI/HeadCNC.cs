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
        internal HeadCNC()
        {
            initialize();
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            var polygon = new Polygon();
            polygon.Points.Add(new Point(-30, 60));
            polygon.Points.Add(new Point(30, 60));
            polygon.Points.Add(new Point(0, 0));

            var fillBrush = new SolidColorBrush(Colors.Red);
            fillBrush.Opacity = 0.3;
            polygon.Fill = fillBrush;
            return polygon;
        }
    }
}
