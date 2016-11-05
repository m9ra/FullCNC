using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
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

        /// <summary>
        /// Size of the workplace (Hack due to measure/arrangement limits).
        /// Can be accessed from arrange/measure of children.
        /// </summary>
        internal Size Size;

        /// <summary>
        /// Item that is moved by using drag and drop
        /// </summary>
        private TrajectoryShapeItem _draggedItem = null;

        /// <summary>
        /// Last position of mouse
        /// </summary>
        private Point _lastMousePosition;

        /// <summary>
        /// Determine whether changes through the workspace are allowed.
        /// </summary>
        private bool _changesDisabled = false;

        internal WorkspacePanel(int stepCountX, int stepCountY)
        {
            StepCountX = stepCountX;
            StepCountY = stepCountY;

            Background = Brushes.White;

            PreviewMouseUp += _mouseUp;
            PreviewMouseMove += _mouseMove;
        }


        internal void DisableChanges()
        {
            _changesDisabled = true;
        }

        internal void EnableChanges()
        {
            _changesDisabled = false;
        }

        private void _mouseMove(object sender, MouseEventArgs e)
        {
            if (_changesDisabled)
                _draggedItem = null;

            var position = e.GetPosition(this);
            var mouseDelta = position - _lastMousePosition;
            _lastMousePosition = position;

            if (_draggedItem != null)
            {
                _draggedItem.PositionX += (int)(mouseDelta.X / Size.Width * StepCountX);
                _draggedItem.PositionY += (int)(mouseDelta.Y / Size.Height * StepCountY);
            }
        }

        void _mouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
        }

        internal JoinLine GetEntryJoinLine()
        {
            foreach (var child in Children)
            {
                var joinLine = child as JoinLine;
                if (joinLine == null)
                    continue;

                if (joinLine.IsEntryPoint)
                    return joinLine;
            }
            return null;
        }

        /// <inheritdoc/>
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            if (visualAdded != null)
            {
                var shapeItem = visualAdded as TrajectoryShapeItem;
                if (shapeItem != null)
                {
                    shapeItem.PreviewMouseDown += (s, e) => _draggedItem = shapeItem;
                }
            }
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
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
            Size = new Size(finalX, finalY);

            foreach (WorkspaceItem child in Children)
            {
                var positionX = Size.Width * child.PositionX / StepCountX;
                var positionY = Size.Height * child.PositionY / StepCountY;
                if (child is JoinLine)
                    //joins have to be refreshed any time
                    child.InvalidateMeasure();

                child.Measure(Size);
                child.Arrange(new Rect(new Point(positionX, positionY), child.DesiredSize));
            }

            return Size;
        }
    }
}
