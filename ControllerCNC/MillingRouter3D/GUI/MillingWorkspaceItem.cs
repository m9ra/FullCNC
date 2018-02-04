using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MillingRouter3D.GUI
{
    abstract class MillingWorkspaceItem : UserControl, ISerializable
    {
        private bool _isHighlighted;

        /// <summary>
        /// Actual position x in mm.
        /// </summary>
        private double _positionX;

        /// <summary>
        /// Actual position y in mm.
        /// </summary>
        private double _positionY;

        /// <summary>
        /// Name of the item.
        /// </summary>
        new internal readonly ReadableIdentifier Name;

        /// <summary>
        /// Event fired when settings of the item changes.
        /// </summary>
        internal event Action OnSettingsChanged;

        /// <summary>
        /// Position of the item in steps.
        /// </summary>
        internal double PositionX
        {
            get { return _positionX; }
            set
            {
                if (_positionX == value)
                    //nothing changed
                    return;

                _positionX = value;
                fireOnSettingsChanged();
            }
        }

        /// <summary>
        /// Position of the item in steps.
        /// </summary>
        internal double PositionY
        {
            get { return _positionY; }
            set
            {
                if (_positionY == value)
                    //nothing changed
                    return;

                _positionY = value;
                fireOnSettingsChanged();
            }
        }

        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set
            {
                if (_isHighlighted == value)
                    //nothing happened
                    return;

                _isHighlighted = value;
                if (_isHighlighted)
                {
                    var effect = new DropShadowEffect();
                    effect.Color = Colors.Blue;
                    effect.BlurRadius = 10;
                    effect.ShadowDepth = 0;
                    this.Effect = effect;
                }
                else
                {
                    this.Effect = null;
                }

                fireOnSettingsChanged();
            }
        }

        /// <summary>
        /// Creates visual face of the item
        /// </summary>
        /// <returns></returns>
        protected abstract object createContent();

        internal MillingWorkspaceItem(ReadableIdentifier name)
        {
            Name = name;
        }

        internal MillingWorkspaceItem(SerializationInfo info, StreamingContext context)
        {
            _positionX = info.GetInt32("_positionX");
            _positionY = info.GetInt32("_positionY");
            Name = (ReadableIdentifier)info.GetValue("Name", typeof(ReadableIdentifier));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_positionX", _positionX);
            info.AddValue("_positionY", _positionY);
            info.AddValue("Name", Name);
        }

        internal virtual void RecalculateToWorkspace(MillingWorkspacePanel workspace, Size size)
        {
            //nothing to do by default
        }

        /// <summary>
        /// Initializes content of the item.
        /// </summary>
        protected void initialize()
        {
            Content = createContent();
        }

        /// <summary>
        /// Fires event after setting was changed.
        /// </summary>
        protected void fireOnSettingsChanged()
        {
            OnSettingsChanged?.Invoke();

            InvalidateVisual();
            var workspace = Parent as MillingWorkspacePanel;
            if (workspace != null)
                workspace.InvalidateArrange();
        }        
    }
}
