using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

using System.Windows;
using System.Windows.Controls;

namespace ControllerCNC.GUI
{
    [Serializable]
    abstract class WorkspaceItem : UserControl, ISerializable
    {
        /// <summary>
        /// Actual position x in steps.
        /// </summary>
        private int _positionX;

        /// <summary>
        /// Actual position y in steps.
        /// </summary>
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

        internal WorkspaceItem(SerializationInfo info, StreamingContext context)
        {
            _positionX = info.GetInt32("_positionX");
            _positionY = info.GetInt32("_positionY");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_positionX", _positionX);
            info.AddValue("_positionY", _positionY);
        }

        internal virtual void RecalculateToWorkspace(WorkspacePanel workspace, Size size)
        {
            //nothing to do by default
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
