using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    class HeadCNC : WorkspaceItem
    {
        private readonly Color _fillColor;

        private readonly bool _isTopDown;

        internal HeadCNC(Color fillColor, bool isTopDown)
            : base(new ReadableIdentifier("HEAD"))
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

            var yCoord = 50;
            var xCoord = 50;
            var crossWidth = 2;

            fillCrossArm(polygon, xCoord, crossWidth);
            fillCrossArm(polygon, crossWidth, yCoord);


            var fillBrush = new SolidColorBrush(_fillColor);
            fillBrush.Opacity = 0.8;
            polygon.Fill = fillBrush;
            return polygon;
        }

        private static void fillCrossArm(Polygon polygon, int xCoord, int width)
        {
            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(xCoord, 0));
            polygon.Points.Add(new Point(xCoord, width));
            polygon.Points.Add(new Point(0, width));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(xCoord, 0));
            polygon.Points.Add(new Point(xCoord, -width));
            polygon.Points.Add(new Point(0, -width));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(-xCoord, 0));
            polygon.Points.Add(new Point(-xCoord, width));
            polygon.Points.Add(new Point(0, width));
            polygon.Points.Add(new Point(0, 0));

            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(-xCoord, 0));
            polygon.Points.Add(new Point(-xCoord, -width));
            polygon.Points.Add(new Point(0, -width));
            polygon.Points.Add(new Point(0, 0));
        }

    }
}
