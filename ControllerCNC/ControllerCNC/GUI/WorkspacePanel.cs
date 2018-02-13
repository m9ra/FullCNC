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

namespace ControllerCNC.GUI
{
    delegate void WorkspaceItemEvent(WorkspaceItem item);

    public class WorkspacePanel : Panel
    {
        /// <summary>
        /// Head display for UV.
        /// </summary>
        internal readonly HeadCNC HeadUV;

        /// <summary>
        /// Head display for XY.
        /// </summary>
        internal readonly HeadCNC HeadXY;

        /// <summary>
        /// Maximum number of steps in x axis.
        /// </summary>
        internal readonly int StepCountX;

        /// <summary>
        /// Maximum number of steps in y axis.
        /// </summary>
        internal readonly int StepCountY;

        /// <summary>
        /// Maximum number of steps in u axis.
        /// </summary>        
        internal readonly int StepCountU;

        /// <summary>
        /// Maximum number of steps in v axis.
        /// </summary>        
        internal readonly int StepCountV;

        /// <summary>
        /// Entry point of the plan.
        /// </summary>
        internal EntryPoint EntryPoint { get { return _entryPoint; } }

        /// <summary>
        /// Speed that will be used for cutting.
        /// </summary>
        internal Speed CuttingSpeed
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
        private Speed _cuttingSpeed;

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
        private PointProviderItem _draggedItem = null;

        /// <summary>
        /// Last position of mouse
        /// </summary>
        private Point _lastMousePosition;

        /// <summary>
        /// Determine whether changes through the workspace are allowed.
        /// </summary>
        private bool _changesDisabled = false;

        /// <summary>
        /// Joints registered by the workspace.
        /// </summary>
        private readonly List<ItemJoin> _itemJoins = new List<ItemJoin>();

        /// <summary>
        /// Where the plan starts.
        /// </summary>
        private EntryPoint _entryPoint;

        private MousePositionInfo _positionInfo;

        private bool _invalidateArrange = true;

        private bool _isArrangeInitialized = false;

        private static readonly Color _uvColor = Colors.Blue;

        private static readonly Color _xyColor = Colors.Red;

        private static readonly Pen _joinPenUV = new Pen(new SolidColorBrush(_uvColor), 2.0);

        private static readonly Pen _joinPenXY = new Pen(new SolidColorBrush(_xyColor), 2.0);

        internal event Action OnSettingsChanged;

        internal event Action OnWorkItemListChanged;

        internal event WorkspaceItemEvent OnWorkItemClicked;

        internal WorkspacePanel(int stepCountC1, int stepCountC2)
        {
            HeadUV = new HeadCNC(_uvColor, this);
            HeadXY = new HeadCNC(_xyColor, this);

            StepCountU = StepCountX = stepCountC1;
            StepCountV = StepCountY = stepCountC2;

            Background = Brushes.White;

            _entryPoint = new EntryPoint();
            Children.Add(_entryPoint);

            _positionInfo = new MousePositionInfo();
            Children.Add(_positionInfo);

            PreviewMouseUp += _mouseUp;
            PreviewMouseDown += _mouseDown;
            PreviewMouseMove += _mouseMove;

            MouseLeave += (s, o) => _positionInfo.Hide();
            MouseEnter += (s, o) => _positionInfo.Show();

            CuttingSpeed = Speed.FromDeltaT(6000);
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
            var itemsToSave = new List<PointProviderItem>();
            foreach (var child in Children)
            {
                if (child is PointProviderItem)
                    itemsToSave.Add(child as PointProviderItem);
            }
            var formatter = new BinaryFormatter();
            var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);

            var configuration = new Dictionary<string, object>();
            configuration.Add("CuttingSpeed", CuttingSpeed);
            configuration.Add("CuttingKerf", CuttingKerf);
            configuration.Add("WireLength", WireLength);
            var workspaceRepresentation = Tuple.Create<List<PointProviderItem>, List<ItemJoin>, Dictionary<string, object>>(itemsToSave, _itemJoins, configuration);
            formatter.Serialize(stream, workspaceRepresentation);
            stream.Close();
        }

        internal void LoadFrom(string filename)
        {
            var formatter = new BinaryFormatter();

            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var workspaceRepresentation = (Tuple<List<PointProviderItem>, List<ItemJoin>, Dictionary<string, object>>)formatter.Deserialize(stream);

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
                _cuttingSpeed = (Speed)configuration["CuttingSpeed"];

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

        public ReadableIdentifier UnusedVersion(ReadableIdentifier name)
        {
            var names = new HashSet<ReadableIdentifier>();
            foreach (var child in Children)
            {
                var item = child as WorkspaceItem;
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

        /// <summary>
        /// Builds plan configured by the workspace. 
        /// ASSUMING the starting position be correctly set up on <see cref="EntryPoint"/>.
        /// </summary>
        internal void BuildPlan(PlanBuilder plan)
        {
            var speedPoints = new List<Speed4Dstep>();
            _entryPoint.Build(this, speedPoints, null);

            var planPoints = speedPoints.Select(p => p.Point).ToList();
            var segmentSpeeds = speedPoints.Select(p => Tuple.Create(p.SpeedUV, p.SpeedXY)).ToArray();

            //scheduler needs referential point
            planPoints.Insert(0, _entryPoint.CutPoints.First());

            var scheduler = new StraightLinePlanner4D(CuttingSpeed);
            var trajectoryPlan = scheduler.CreateConstantPlan(new Trajectory4D(planPoints), segmentSpeeds);

            plan.Add(trajectoryPlan.Build());
        }

        internal IEnumerable<ItemJoin> GetOutgoingJoinsFrom(int currentIndex, IEnumerable<ItemJoin> outgoingJoins)
        {
            var result = new List<ItemJoin>();
            foreach (var join in outgoingJoins)
            {
                if (join.JoinPointIndex1 == currentIndex)
                    result.Add(join);
            }

            return result;
        }

        internal IEnumerable<ItemJoin> FindOutgoingJoins(PointProviderItem item)
        {
            var result = new List<ItemJoin>();
            foreach (var join in _itemJoins)
            {
                if (join.Item1 == item)
                    result.Add(join);
            }

            return result;
        }

        internal IEnumerable<ItemJoin> GetIncomingJoins(WorkspaceItem item)
        {
            foreach (var join in _itemJoins)
                if (join.Item2 == item)
                    yield return join;
        }

        internal void SetJoin(PointProviderItem shape1, int joinPointIndex1, PointProviderItem shape2, int joinPointIndex2)
        {
            if (shape1 is ScaffoldItem || shape2 is ScaffoldItem)
                //scaffold cannot be joined
                return;

            if (shape1 is NativeControlItem || (shape2 is NativeControlItem && !(shape1 is EntryPoint)))
                //native items cant be joined with other items
                return;

            if (shape2 is NativeControlItem)
                //native items can run only separately
                _itemJoins.Clear();

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

            var newJoin = new ItemJoin(shape1, joinPointIndex1, shape2, joinPointIndex2);
            _itemJoins.Add(newJoin);

            InvalidateVisual();
            fireOnSettingsChanged();
            fireOnWorkItemListChanged();
        }

        internal void SetJoin(PointProviderItem shape1, PointProviderItem shape2)
        {
            var points1 = shape1.CutPoints.ToArray();
            var points2 = shape2.CutPoints.ToArray();

            var best1 = 0;
            var best2 = 0;
            var bestDistance = double.PositiveInfinity;
            for (var i = 0; i < points1.Length; ++i)
            {
                var point1 = points1[i];
                for (var j = 0; j < points2.Length; ++j)
                {
                    var point2 = points2[j];
                    var distance = point1.DistanceSquaredTo(point2);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best1 = i;
                        best2 = j;
                    }
                }
            }

            if (shape2 is NativeControlItem)
                best2 = 0;

            SetJoin(shape1, best1, shape2, best2);
        }

        internal void RemoveJoin(ItemJoin join)
        {
            _itemJoins.Remove(join);

            InvalidateVisual();
            fireOnWorkItemListChanged();
            fireOnSettingsChanged();
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
                _draggedItem.PositionC1 += (int)(mouseDelta.X / ActualWidth * StepCountX);
                _draggedItem.PositionC2 += (int)(mouseDelta.Y / ActualHeight * StepCountY);
            }

            _positionInfo.PositionC1 = (int)(position.X / ActualWidth * StepCountX);
            _positionInfo.PositionC2 = (int)(position.Y / ActualHeight * StepCountY);
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
                return;
            }

            if (_lastScaffold == null || !Children.Contains(_lastScaffold))
                _lastScaffold = null;


            if (_lastScaffold == null)
                _lastScaffold = new ScaffoldItem(UnusedVersion(new ReadableIdentifier("scaffold")), new Point2Dstep[0]);
            else
            {
                Children.Remove(_lastScaffold);
            }

            var position = e.GetPosition(this);
            var stepsX = (int)(position.X / ActualWidth * StepCountX);
            var stepsY = (int)(position.Y / ActualHeight * StepCountY);
            _lastScaffold = _lastScaffold.ExtendBy(new Point2Dstep(stepsX, stepsY));

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
                var startPointUV = getJoinPointProjectedUV(join.Item1, join.JoinPointIndex1);
                var endPointUV = getJoinPointProjectedUV(join.Item2, join.JoinPointIndex2);
                var startPointXY = getJoinPointProjectedXY(join.Item1, join.JoinPointIndex1);
                var endPointXY = getJoinPointProjectedXY(join.Item2, join.JoinPointIndex2);

                var geometryUV = CreateLinkArrow(startPointUV, endPointUV);
                var geometryXY = CreateLinkArrow(startPointXY, endPointXY);
                dc.DrawGeometry(null, _joinPenUV, geometryUV);
                dc.DrawGeometry(null, _joinPenXY, geometryXY);
            }

            HeadUV.Draw(dc);
            HeadXY.Draw(dc);
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
                var pointProvider = visualAdded as PointProviderItem;
                if (pointProvider != null)
                {
                    //enable drag 
                    pointProvider.PreviewMouseLeftButtonDown += (s, e) => _draggedItem = pointProvider;
                    //enable properties dialog
                    pointProvider.MouseRightButtonUp += (s, e) => new PointProviderPropertiesDialog(pointProvider);
                }

                //setup change listener to work items
                var workItem = visualAdded as WorkspaceItem;
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

        private void fireOnSettingsChanged()
        {
            OnSettingsChanged?.Invoke();
        }

        private void fireOnWorkitemClicked(WorkspaceItem item)
        {
            OnWorkItemClicked?.Invoke(item);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!_invalidateArrange)
                return DesiredSize;

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
            if (!_invalidateArrange)
            {
                _invalidateArrange = true;
                return this.DesiredSize;
            }

            InvalidateVisual();
            finalSize = this.DesiredSize;
            foreach (WorkspaceItem child in Children)
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

        private double projectToX(Size finalSize, WorkspaceItem child)
        {
            var positionX = finalSize.Width * child.PositionC1 / StepCountX;
            return positionX;
        }

        private double projectToY(Size finalSize, WorkspaceItem child)
        {
            var positionX = finalSize.Height * child.PositionC2 / StepCountY;
            return positionX;
        }

        private Point getJoinPointProjectedUV(PointProviderItem item, int pointIndex)
        {
            var point4D = item.CutPoints.Skip(pointIndex).First();

            var coordU = ActualWidth * point4D.U / StepCountU;
            var coordV = ActualHeight * point4D.V / StepCountV;

            return new Point(coordU, coordV);
        }

        private Point getJoinPointProjectedXY(PointProviderItem item, int pointIndex)
        {
            var point4D = item.CutPoints.Skip(pointIndex).First();

            var coordX = ActualWidth * point4D.X / StepCountX;
            var coordY = ActualHeight * point4D.Y / StepCountY;

            return new Point(coordX, coordY);
        }

        public static Geometry CreateLinkArrow(Point p1, Point p2)
        {
            var lineGroup = new GeometryGroup();
            var theta = Math.Atan2((p2.Y - p1.Y), (p2.X - p1.X)) * 180 / Math.PI;

            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();
            var p = new Point(p1.X + ((p2.X - p1.X) / 1.35), p1.Y + ((p2.Y - p1.Y) / 1.35));
            pathFigure.StartPoint = p;

            var lpoint = new Point(p.X + 6, p.Y + 15);
            var rpoint = new Point(p.X - 6, p.Y + 15);
            var seg1 = new LineSegment();
            seg1.Point = lpoint;
            pathFigure.Segments.Add(seg1);

            var seg2 = new LineSegment();
            seg2.Point = rpoint;
            pathFigure.Segments.Add(seg2);

            var seg3 = new LineSegment();
            seg3.Point = p;
            pathFigure.Segments.Add(seg3);

            pathGeometry.Figures.Add(pathFigure);
            var transform = new RotateTransform();
            transform.Angle = theta + 90;
            transform.CenterX = p.X;
            transform.CenterY = p.Y;
            pathGeometry.Transform = transform;
            lineGroup.Children.Add(pathGeometry);

            var connectorGeometry = new LineGeometry();
            connectorGeometry.StartPoint = p1;
            connectorGeometry.EndPoint = p2;
            lineGroup.Children.Add(connectorGeometry);
            return lineGroup;
        }
    }
}
