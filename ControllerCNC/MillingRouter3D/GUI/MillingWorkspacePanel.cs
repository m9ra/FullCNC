using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;


using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;
using ControllerCNC.GUI;

using MillingRouter3D.Primitives;

namespace MillingRouter3D.GUI
{
    delegate void MillingWorkspaceItemEvent(MillingWorkspaceItem item);

    class MillingWorkspacePanel : Panel
    {
        /// <summary>
        /// Head display for XYZ.
        /// </summary>
        internal readonly HeadCNC HeadXYZ;

        /// <summary>
        /// Range on X axis.
        /// </summary>
        internal readonly double RangeX;

        /// <summary>
        /// Range on Y axis.
        /// </summary>
        internal readonly double RangeY;

        /// <summary>
        /// Range on Z axis.
        /// </summary>        
        internal readonly double RangeZ;

        /// <summary>
        /// Entry point of the plan.
        /// </summary>
        internal EntryPoint EntryPoint { get { return _entryPoint; } }

        /// <summary>
        /// Speed that will be used for cutting.
        /// </summary>
        internal double CuttingSpeedMm
        {
            get { return _cuttingSpeed; }
            set
            {
                if (_cuttingSpeed == value)
                    //nothing has changed
                    return;

                _cuttingSpeed = value;
                fireSettingsChangedForAllChildren();
            }
        }

        internal double MaxLayerCut
        {
            get { return _maxLayerCut; }
            set
            {
                if (_maxLayerCut == value)
                    //nothing has changed
                    return;

                _maxLayerCut = value;
                fireSettingsChangedForAllChildren();
            }
        }


        internal Speed CuttingSpeed => Speed.FromMilimetersPerSecond(CuttingSpeedMm);

        /// <summary>
        /// Wire setup for the workspace.
        /// </summary>
        internal double WireLength
        {
            get
            {
                return _wireLength;
            }

            set
            {
                if (value == _wireLength)
                    //nothing has changed
                    return;

                _wireLength = value;
                fireOnSettingsChanged();
                this.InvalidateVisual();
                foreach (FrameworkElement child in Children)
                {
                    child.InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Kerf setup for the workspace.
        /// </summary>
        internal double CuttingKerf
        {
            get
            {
                return _cuttingKerf;
            }

            set
            {
                if (value == _cuttingKerf)
                    //nothing has changed
                    return;

                _cuttingKerf = value;
                fireSettingsChangedForAllChildren();
            }
        }

        /// <summary>
        /// Determine whether scaffold mode is enabled.
        /// </summary>
        internal bool ScaffoldModeEnabled { get; set; }

        /// <summary>
        /// Speed that will be used for cutting.
        /// </summary>
        private double _cuttingSpeed;

        /// <summary>
        /// Maximum cut depth for a single layer.
        /// </summary>
        private double _maxLayerCut;

        /// <summary>
        /// Kerf for cutting.
        /// </summary>
        private double _cuttingKerf;

        /// <summary>
        /// Length of wire for cutting.
        /// </summary>
        private double _wireLength;

        /// <summary>
        /// Lastly modified scaffold.
        /// </summary>
        private ScaffoldItem _lastScaffold;

        /// <summary>
        /// Item that is moved by using drag and drop
        /// </summary>
        private MillingWorkspaceItem _draggedItem = null;

        /// <summary>
        /// Last position of mouse
        /// </summary>
        private Point _lastMousePosition;

        /// <summary>
        /// Determine whether changes through the workspace are allowed.
        /// </summary>
        private bool _changesDisabled = false;

        /// <summary>
        /// Where the plan starts.
        /// </summary>
        private EntryPoint _entryPoint;

        private MousePositionInfo _positionInfo;

        private bool _invalidateArrange = true;

        private bool _isArrangeInitialized = false;

        private readonly List<MillingJoin> _itemJoins = new List<MillingJoin>();

        private static readonly Color _xyColor = Colors.Red;

        private static readonly Pen _joinPenXY = new Pen(new SolidColorBrush(_xyColor), 2.0);

        public event Action OnSettingsChanged;

        public event Action OnWorkItemListChanged;

        public event MillingWorkspaceItemEvent OnWorkItemClicked;

        internal MillingWorkspacePanel(RouterPanel parent, double rangeX, double rangeY, double rangeZ)
        {
            HeadXYZ = new HeadCNC(_xyColor, this);
            Background = Brushes.White;
            RangeX = Math.Abs(rangeX);
            RangeY = Math.Abs(rangeY);
            RangeZ = Math.Abs(rangeZ);

            _entryPoint = new EntryPoint();
            Children.Add(_entryPoint);

            _positionInfo = new MousePositionInfo();
            Children.Add(_positionInfo);

            PreviewMouseUp += _mouseUp;
            PreviewMouseDown += _mouseDown;
            PreviewMouseMove += _mouseMove;

            MouseLeave += (s, o) => _positionInfo.Hide();
            MouseEnter += (s, o) => _positionInfo.Show();

            CuttingSpeedMm = 1.0;
            MaxLayerCut = 2.0;
            WireLength = Configuration.FullWireLength;
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

        internal void InvalidateVisualOnly()
        {
            InvalidateVisual();
            if (_isArrangeInitialized)
                _invalidateArrange = false;
        }

        internal void SaveTo(string filename)
        {
            var itemsToSave = new List<PermanentMillingWorkspaceItem>();
            foreach (var child in Children)
            {
                if (child is PermanentMillingWorkspaceItem)
                    itemsToSave.Add(child as PermanentMillingWorkspaceItem);
            }
            var formatter = new BinaryFormatter();
            var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);

            var configuration = new Dictionary<string, object>();
            configuration.Add("MaxLayerCut", MaxLayerCut);
            configuration.Add("CuttingSpeed", CuttingSpeedMm);
            configuration.Add("CuttingKerf", CuttingKerf);
            configuration.Add("WireLength", WireLength);
            var workspaceRepresentation = Tuple.Create<List<PermanentMillingWorkspaceItem>, List<MillingJoin>, Dictionary<string, object>>(itemsToSave, _itemJoins, configuration);
            formatter.Serialize(stream, workspaceRepresentation);
            stream.Close();
        }

        internal void LoadFrom(string filename)
        {
            var formatter = new BinaryFormatter();

            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var workspaceRepresentation = (Tuple<List<PermanentMillingWorkspaceItem>, List<MillingJoin>, Dictionary<string, object>>)formatter.Deserialize(stream);

                Children.Clear();
                Children.Add(_positionInfo);

                _itemJoins.Clear();

                foreach (var item in workspaceRepresentation.Item1)
                {
                    if (item is EntryPoint)
                        _entryPoint = item as EntryPoint;

                    Children.Add(item);
                }

                foreach (var join in workspaceRepresentation.Item2)
                {
                    _itemJoins.Add(join);
                }

                var configuration = workspaceRepresentation.Item3;
                _cuttingSpeed = (double)configuration["CuttingSpeed"];

                if (configuration.ContainsKey("MaxLayerCut"))
                    _maxLayerCut = (double)configuration["MaxLayerCut"];

                if (configuration.ContainsKey("CuttingKerf"))
                    _cuttingKerf = (double)configuration["CuttingKerf"];

                if (configuration.ContainsKey("WireLength"))
                    _wireLength = (double)configuration["WireLength"];
                else
                    _wireLength = Configuration.FullWireLength;

                fireOnWorkItemListChanged();
                fireOnSettingsChanged();
            }
        }

        internal ReadableIdentifier UnusedVersion(ReadableIdentifier name)
        {
            var names = new HashSet<ReadableIdentifier>();
            foreach (var child in Children)
            {
                var item = child as MillingWorkspaceItem;
                if (item == null)
                    continue;

                names.Add(item.Name);
            }

            var currentVersion = name;
            while (names.Contains(currentVersion))
            {
                currentVersion = currentVersion.NextVersion();
            }
            return currentVersion;
        }

        internal void SetJoin(MillingItem shape1, MillingItem shape2)
        {
            var joinCopy = _itemJoins.ToArray();
            foreach (var join in joinCopy)
            {
                if (join.Item2 == shape2)
                    //shape can have only one target join
                    _itemJoins.Remove(join);

                if (join.Item1 == shape2 && join.Item2 == shape1)
                    //cyclic joins are not allowes
                    _itemJoins.Remove(join);
            }

            var newJoin = new MillingJoin(shape1, shape2);
            _itemJoins.Add(newJoin);

            InvalidateVisual();
            fireOnSettingsChanged();
            fireOnWorkItemListChanged();
        }

        internal IEnumerable<MillingJoin> FindOutgoingJoins(MillingItem item)
        {
            var result = new List<MillingJoin>();
            foreach (var join in _itemJoins)
            {
                if (join.Item1 == item)
                    result.Add(join);
            }

            return result;
        }

        internal IEnumerable<MillingJoin> GetIncomingJoins(MillingWorkspaceItem item)
        {
            foreach (var join in _itemJoins)
                if (join.Item2 == item)
                    yield return join;
        }

        /// <summary>
        /// Builds plan configured by the workspace. 
        /// ASSUMING the starting position be correctly set up on <see cref="EntryPoint"/>.
        /// </summary>
        internal void BuildPlan(PlanBuilder3D builder)
        {
            EntryPoint.BuildPlan(builder, this);
        }

        internal void RefreshJoins()
        {
            foreach (var join in _itemJoins.ToArray())
            {
                SetJoin(join.Item1, join.Item2);
            }
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
                _draggedItem.PositionX += mouseDelta.X / ActualWidth * RangeX;
                _draggedItem.PositionY += mouseDelta.Y / ActualHeight * RangeY;
            }

            _positionInfo.PositionX = position.X / ActualWidth * RangeX;
            _positionInfo.PositionY = position.Y / ActualHeight * RangeY;
            _positionInfo.UpdateInfo();
        }

        /// <summary>
        /// Handler for releasing dragged objects.
        /// </summary>
        private void _mouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
        }

        /// <summary>
        /// Handler for scaffold mode.
        /// </summary>
        private void _mouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!ScaffoldModeEnabled)
                return;

            if (e.RightButton == MouseButtonState.Pressed)
            {
                _lastScaffold = null;
                e.Handled = true;
                return;
            }

            if (_lastScaffold == null || !Children.Contains(_lastScaffold))
                _lastScaffold = null;


            if (_lastScaffold == null)
                _lastScaffold = new ScaffoldItem(UnusedVersion(new ReadableIdentifier("scaffold")), new Point2Dmm[0]);
            else
            {
                Children.Remove(_lastScaffold);
            }

            var position = e.GetPosition(this);
            var mmX = position.X / ActualWidth * RangeX;
            var mmY = position.Y / ActualHeight * RangeY;
            _lastScaffold = _lastScaffold.ExtendBy(new Point2Dmm(mmX, mmY));

            Children.Add(_lastScaffold);
        }

        #endregion

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            //render join lines
            foreach (var join in _itemJoins)
            {
                var startPointXY = getJoinPointProjected(join.Item1);
                var endPointXY = getJoinPointProjected(join.Item2);

                var geometryXY = WorkspacePanel.CreateLinkArrow(startPointXY, endPointXY);
                dc.DrawGeometry(null, _joinPenXY, geometryXY);
            }

            HeadXYZ.Draw(dc);
        }

        private Point getJoinPointProjected(MillingItem item)
        {
            var point = item.EntryPoint;

            var x = ActualWidth * point.C1 / RangeX;
            var y = ActualHeight * point.C2 / RangeY;

            return new Point(x, y);
        }


        /// <inheritdoc/>
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            if (visualRemoved != null)
            {
                //TODO cleanup handlers 
                foreach (var join in _itemJoins.ToArray())
                {
                    if (join.Item1 == visualRemoved || join.Item2 == visualRemoved)
                        _itemJoins.Remove(join);
                }
            }

            if (visualAdded != null)
            {
                var millingItem = visualAdded as MillingWorkspaceItem;
                if (millingItem != null)
                {
                    //enable drag 
                    millingItem.PreviewMouseLeftButtonDown += (s, e) => _draggedItem = millingItem;
                    //enable properties dialog
                    millingItem.MouseRightButtonUp += (s, e) => new MillingItemPropertiesDialog(millingItem, this);
                }

                //setup change listener to work items
                var workItem = visualAdded as MillingWorkspaceItem;
                if (workItem != null)
                {
                    workItem.OnSettingsChanged += fireOnSettingsChanged;
                    workItem.MouseUp += (s, e) => fireOnWorkitemClicked(workItem);
                }
            }
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);

            fireOnWorkItemListChanged();
            fireOnSettingsChanged();
        }

        private void fireOnWorkItemListChanged()
        {
            OnWorkItemListChanged?.Invoke();
        }

        private void fireSettingsChangedForAllChildren()
        {
            fireOnSettingsChanged();
            this.InvalidateVisual();
            foreach (FrameworkElement child in Children)
            {
                child.InvalidateVisual();
            }
        }

        internal void RemoveJoin(MillingJoin join)
        {
            _itemJoins.Remove(join);

            InvalidateVisual();
            fireOnWorkItemListChanged();
            fireOnSettingsChanged();
        }

        private void fireOnSettingsChanged()
        {
            OnSettingsChanged?.Invoke();
        }

        private void fireOnWorkitemClicked(MillingWorkspaceItem item)
        {
            OnWorkItemClicked?.Invoke(item);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!_invalidateArrange)
                return DesiredSize;

            var ratioX = 1.0 * RangeX / RangeY;
            var ratioY = 1.0 * RangeY / RangeX;

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
            if (!_invalidateArrange)
            {
                _invalidateArrange = true;
                return this.DesiredSize;
            }

            InvalidateVisual();
            finalSize = this.DesiredSize;
            foreach (MillingWorkspaceItem child in Children)
            {
                var positionX = projectToX(finalSize, child);
                var positionY = projectToY(finalSize, child);
                child.RecalculateToWorkspace(this, finalSize);
                child.Measure(finalSize);
                child.Arrange(new Rect(new Point(positionX, positionY), child.DesiredSize));
            }

            _isArrangeInitialized = true;
            return finalSize;
        }

        private double projectToX(Size finalSize, MillingWorkspaceItem child)
        {
            var positionX = finalSize.Width * child.PositionX / RangeX;
            return positionX;
        }

        private double projectToY(Size finalSize, MillingWorkspaceItem child)
        {
            var positionX = finalSize.Height * child.PositionY / RangeY;
            return positionX;
        }
    }
}
