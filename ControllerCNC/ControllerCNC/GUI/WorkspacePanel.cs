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

        /// <summary>
        /// Prevents user for making changes through the workspace panel.
        /// </summary>
        internal void DisableChanges()
        {
            _changesDisabled = true;
        }

        /// <summary>
        /// Enables user to make changes through the workspace panel.
        /// </summary>
        internal void EnableChanges()
        {
            _changesDisabled = false;
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

        #region Drag and drop handlers

        /// <summary>
        /// Handler for moving mouse (calculates delta for drag and drop)
        /// </summary>
        private void _mouseMove(object sender, MouseEventArgs e)
        {
            if (_changesDisabled)
                _draggedItem = null;

            var position = e.GetPosition(this);
            var mouseDelta = position - _lastMousePosition;
            _lastMousePosition = position;

            if (_draggedItem != null)
            {
                _draggedItem.PositionX += (int)(mouseDelta.X / ActualWidth * StepCountX);
                _draggedItem.PositionY += (int)(mouseDelta.Y / ActualHeight * StepCountY);
            }
        }

        /// <summary>
        /// Handler for releasing dragged objects.
        /// </summary>
        private void _mouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
        }

        #endregion

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
            var ratioX = 1.0 * StepCountX / StepCountY;
            var ratioY = 1.0 * StepCountY / StepCountX;

            var finalY = ratioY * availableSize.Width;
            var finalX = ratioX * availableSize.Height;

            if (finalX > availableSize.Width)
            {
                finalX = availableSize.Width;
            }
            else
            {
                finalY = availableSize.Height;
            }
            var size = new Size(finalX, finalY);
            return size;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            finalSize = this.DesiredSize;
            foreach (WorkspaceItem child in Children)
            {
                var positionX = finalSize.Width * child.PositionX / StepCountX;
                var positionY = finalSize.Height * child.PositionY / StepCountY;
                if (child is JoinLine)
                    //joins have to be refreshed any time
                    child.InvalidateMeasure();

                child.RegisterWorkspaceSize(finalSize);
                child.Measure(finalSize);
                child.Arrange(new Rect(new Point(positionX, positionY), child.DesiredSize));
            }

            return finalSize;
        }
    }
}
