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
    class HeadCNC
    {
        /// <summary>
        /// Color of the head.
        /// </summary>
        private readonly Pen _headPen;

        /// <summary>
        /// Actual position in mm.
        /// </summary>
        private Point2Dmm _position;

        /// <summary>
        /// Parent panel.
        /// </summary>
        private readonly WorkspacePanel _parent;

        /// <summary>
        /// Actual position in mm.
        /// </summary>
        internal Point2Dmm Position
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
                _parent.InvalidateVisual();
            }
        }

        internal HeadCNC(Color headColor, WorkspacePanel parent)
        {
            var brush = new SolidColorBrush(headColor);
            brush.Opacity = 0.8;
            _headPen = new Pen(brush, 4.0);
            _parent = parent;

            Position = new Point2Dmm(0, 0);
        }

        internal void Draw(DrawingContext dc)
        {
            var armLength = 50;
            var crossWidth = 2;

            var mmToStep = Machine.Constants.MilimetersPerStep;
            var c1Factor = _parent.ActualWidth / _parent.StepCountU / mmToStep;
            var c2Factor = _parent.ActualHeight / _parent.StepCountV / mmToStep;

            var c1 = Position.C1 * c1Factor;
            var c2 = Position.C2 * c2Factor;
            dc.DrawLine(_headPen, new Point(-armLength + c1, 0 + c2), new Point(-crossWidth + c1, 0 + c2));
            dc.DrawLine(_headPen, new Point(armLength + c1, 0 + c2), new Point(crossWidth + c1, 0 + c2));
            dc.DrawLine(_headPen, new Point(0 + c1, -armLength + c2), new Point(0 + c1, -crossWidth + c2));
            dc.DrawLine(_headPen, new Point(0 + c1, armLength + c2), new Point(0 + c1, crossWidth + c2));
        }
    }
}
