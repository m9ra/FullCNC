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
        protected abstract object createContent();


        internal WorkspaceItem()
        {
        }

        protected void initialize()
        {
            Content = createContent();
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
