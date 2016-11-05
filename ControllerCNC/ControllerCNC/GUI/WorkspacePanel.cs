using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace ControllerCNC.GUI
{
    class WorkspacePanel : Panel
    {
        /// <summary>
        /// Maximum number of steps in x axis.
        /// </summary>
        internal readonly int StepCountX;

        /// <summary>
        /// Maximum number of steps in y axis.
        /// </summary>
        internal readonly int StepCountY;

        internal WorkspacePanel(int stepCountX, int stepCountY)
        {
            StepCountX = stepCountX;
            StepCountY = stepCountY;

            Background = Brushes.White;
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (WorkspaceItem child in Children)
                child.Measure(availableSize);

            return availableSize;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            var ratioX = 1.0 * StepCountX / StepCountY;
            var ratioY = 1.0 * StepCountY / StepCountX;

            var finalY = ratioY * finalSize.Width;
            var finalX = ratioX * finalSize.Height;

            if (finalX > finalSize.Width)
            {
                finalX = finalSize.Width;
            }
            else
            {
                finalY = finalSize.Height;
            }
            var arrangedSize = new Size(finalX, finalY);

            foreach (WorkspaceItem child in Children)
            {
                var positionX = arrangedSize.Width * child.PositionX / StepCountX + child.VisualOffsetX;
                var positionY = arrangedSize.Height * child.PositionY / StepCountY + child.VisualOffsetY;
                child.Arrange(new Rect(new Point(positionX, positionY), finalSize));
            }

            return arrangedSize;
        }
    }
}
