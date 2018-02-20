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
    abstract class PermanentMillingWorkspaceItem : MillingWorkspaceItem
    {
        internal PermanentMillingWorkspaceItem(ReadableIdentifier name) : base(name)
        {
        }

        internal PermanentMillingWorkspaceItem(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

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
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        protected double _mmToVisualFactorC1;

        /// <summary>
        /// Factor which gives ratio between single step and visual size.
        /// </summary>
        protected double _mmToVisualFactorC2;

        /// <summary>
        /// Angle of rotation [0..360)degree
        /// </summary>
        protected double _rotationAngle;

        /// <summary>
        /// Sin of the current rotation.
        /// </summary>
        protected double _rotationSin;

        /// <summary>
        /// Cos of the current rotation.
        /// </summary>
        protected double _rotationCos;

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

        /// <summary>
        /// Rotation in degrees.
        /// </summary>
        internal double RotationAngle
        {
            get
            {
                return _rotationAngle;
            }

            set
            {
                if (value == _rotationAngle)
                    return;

                _rotationAngle = value;
                _rotationSin = Math.Sin(_rotationAngle / 360 * 2 * Math.PI);
                _rotationCos = Math.Cos(_rotationAngle / 360 * 2 * Math.PI);
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
            constructionInitialization();
        }

        internal MillingWorkspaceItem(SerializationInfo info, StreamingContext context)
        {
            _positionX = info.GetInt32("_positionX");
            _positionY = info.GetInt32("_positionY");
            _rotationAngle = info.GetDouble("_rotationAngle");
            Name = (ReadableIdentifier)info.GetValue("Name", typeof(ReadableIdentifier));
            constructionInitialization();
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_positionX", _positionX);
            info.AddValue("_positionY", _positionY);
            info.AddValue("_rotationAngle", _rotationAngle);
            info.AddValue("Name", Name);
        }

        /// <inheritdoc/>
        internal virtual void RecalculateToWorkspace(MillingWorkspacePanel workspace, Size size)
        {
            _mmToVisualFactorC1 = size.Width / workspace.RangeX;
            _mmToVisualFactorC2 = size.Height / workspace.RangeY;
        }

        /// <summary>
        /// Creates figure by joining the given points.
        /// </summary>
        protected PathGeometry CreatePathGeometry(IEnumerable<Point2Dmm[]> geometryPointClusters)
        {
            var figures = new List<PathFigure>();
            foreach (var geometryPoints in geometryPointClusters)
            {
                PathFigure figure = CreatePathFigure(geometryPoints);
                figures.Add(figure);
            }
            var geometry = new PathGeometry(figures, FillRule.EvenOdd, Transform.Identity);
            return geometry;
        }

        protected PathFigure CreatePathFigure(Point2Dmm[] geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var firstPoint = new Point(0, 0);
            var isFirst = true;
            foreach (var point in geometryPoints)
            {
                var visualPoint = ConvertToVisual(point);

                pathSegments.Add(new LineSegment(visualPoint, !isFirst));
                if (isFirst)
                    firstPoint = visualPoint;
                isFirst = false;
            }
            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }

        protected Point ConvertToVisual(Point2Dmm point)
        {
            var visualPoint = new Point(point.C1 - PositionX, point.C2 - PositionY);
            visualPoint.X = visualPoint.X * _mmToVisualFactorC1;
            visualPoint.Y = visualPoint.Y * _mmToVisualFactorC2;
            return visualPoint;
        }

        protected Point ConvertToVisual(Point3Dmm point)
        {
            return ConvertToVisual(new Point2Dmm(point.X, point.Y));
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

        private void constructionInitialization()
        {
            //reset rotation
            var desiredAngle = _rotationAngle;
            RotationAngle = desiredAngle + 1;
            RotationAngle = desiredAngle;
        }
    }
}
