using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using ControllerCNC.Machine;
using System.Windows;

namespace MillingRouter3D.GUI
{
    class HeadCNC
    {
        /// <summary>
        /// Color of the head.
        /// </summary>
        private readonly Pen _headPen;

        /// <summary>
        /// Actual position in mm.
        /// </summary>
        private Point3Dmm _position;

        /// <summary>
        /// Parent panel.
        /// </summary>
        private readonly MillingWorkspacePanel _parent;

        /// <summary>
        /// Actual position in mm.
        /// </summary>
        internal Point3Dmm Position
        {
            get
            {
                return _position;
            }
            set
            {
                if (value.Equals(_position))
                    //nothing has changed
                    return;

                _position = value;
                _parent.InvalidateVisualOnly();
            }
        }

        internal HeadCNC(Color headColor, MillingWorkspacePanel parent)
        {
            var brush = new SolidColorBrush(headColor);
            brush.Opacity = 0.8;
            _headPen = new Pen(brush, 4.0);
            _parent = parent;

            Position = new Point3Dmm(0, 0, 0);
        }

        internal void Draw(DrawingContext dc)
        {
            var armLength = 50;
            var crossWidth = 2;

            var c1Factor = _parent.ActualWidth / _parent.RangeX;
            var c2Factor = _parent.ActualHeight / _parent.RangeY;

            var c1 = Position.X * c1Factor;
            var c2 = Position.Y * c2Factor;
            dc.DrawLine(_headPen, new Point(-armLength + c1, 0 + c2), new Point(-crossWidth + c1, 0 + c2));
            dc.DrawLine(_headPen, new Point(armLength + c1, 0 + c2), new Point(crossWidth + c1, 0 + c2));
            dc.DrawLine(_headPen, new Point(0 + c1, -armLength + c2), new Point(0 + c1, -crossWidth + c2));
            dc.DrawLine(_headPen, new Point(0 + c1, armLength + c2), new Point(0 + c1, crossWidth + c2));
        }
    }
}
