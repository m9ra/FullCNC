using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Controls;

namespace ControllerCNC.GUI
{
    abstract class WorkspaceItem : UserControl
    {
        private int _positionX;
        private int _positionY;

        /// <summary>
        /// Offset of visual representation (in wpf coordinate units).
        /// </summary>
        internal readonly double VisualOffsetX;

        /// <summary>
        /// Offset of visual representation (in wpf coordinate units).
        /// </summary>
        internal readonly double VisualOffsetY;

        /// <summary>
        /// Position of the item in steps.
        /// </summary>
        internal int PositionX
        {
            get { return _positionX; }
            set
            {
                if (_positionX == value)
                    //nothing changed
                    return;

                _positionX = value;
                onPositionChanged();
            }
        }

        /// <summary>
        /// Position of the item in steps.
        /// </summary>
        internal int PositionY
        {
            get { return _positionY; }
            set
            {
                if (_positionY == value)
                    //nothing changed
                    return;
                _positionY = value;
                onPositionChanged();
            }
        }

        /// <summary>
        /// Creates visual face of the item
        /// </summary>
        /// <returns></returns>
        protected abstract object createContent(out double visualOffsetX, out double visualOffsetY);


        internal WorkspaceItem()
        {
            Content = createContent(out VisualOffsetX, out VisualOffsetY);
        }


        /// <summary>
        /// Handle change in position.
        /// </summary>
        private void onPositionChanged()
        {
            var workspace = Parent as WorkspacePanel;
            if (workspace != null)
                workspace.InvalidateArrange();
        }
    }
}
