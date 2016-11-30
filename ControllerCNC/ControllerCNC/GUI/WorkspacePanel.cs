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
    internal delegate void WorkspaceItemEvent(WorkspaceItem item);

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

        internal readonly int StepCountU;

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
                    //nothing to do
                    return;

                _cuttingSpeed = value;
                fireOnSettingsChanged();
            }
        }

        /// <summary>
        /// Speed that will be used for cutting.
        /// </summary>
        private Speed _cuttingSpeed;

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

        private readonly Pen _joinPenUV = new Pen(Brushes.Blue, 2.0);

        private readonly Pen _joinPenXY = new Pen(Brushes.Red, 2.0);

        internal event Action OnSettingsChanged;

        internal event Action OnWorkItemListChanged;

        internal event WorkspaceItemEvent OnWorkItemClicked;

        internal WorkspacePanel(int stepCountC1, int stepCountC2)
        {
            StepCountU = StepCountX = stepCountC1;
            StepCountV = StepCountY = stepCountC2;

            Background = Brushes.White;

            PreviewMouseUp += _mouseUp;
            PreviewMouseMove += _mouseMove;
            CuttingSpeed = Speed.FromDeltaT(6000);

            _entryPoint = new EntryPoint();
            Children.Add(_entryPoint);
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
            var workspaceRepresentation = Tuple.Create<List<PointProviderItem>, List<ItemJoin>, Dictionary<string, object>>(itemsToSave, _itemJoins, configuration);
            formatter.Serialize(stream, workspaceRepresentation);
            stream.Close();
        }

        internal void LoadFrom(string filename)
        {
            var formatter = new BinaryFormatter();
            var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            var workspaceRepresentation = (Tuple<List<PointProviderItem>, List<ItemJoin>, Dictionary<string, object>>)formatter.Deserialize(stream);
            stream.Close();

            Children.Clear();
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

            fireOnWorkItemListChanged();
            fireOnSettingsChanged();
        }

        internal ReadableIdentifier UnusedVersion(ReadableIdentifier name)
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
            var planPoints = new List<Point4Dstep>();

            //the plan starts from entry point (recursively)
            fillPlanBy(planPoints, _entryPoint, 0);

            var scheduler = new StraightLinePlanner4D(CuttingSpeed);
            var trajectoryPlan = scheduler.CreateConstantPlan(new Trajectory4D(planPoints));

            plan.Add(trajectoryPlan.Build());
        }

        private void fillPlanBy(List<Point4Dstep> planPoints, PointProviderItem item, int incommingPointIndex)
        {
            var points = item.CutPoints.ToArray();
            var isClosedShape = points.First().Equals(points.Last());
            if (!isClosedShape)
                throw new NotImplementedException("Plan non-closed shape items.");

            var outgoingJoins = findOutgoingJoins(item);
            //we have to go one point further (to have the shape closed)
            for (var i = incommingPointIndex; i < incommingPointIndex + points.Length + 1; ++i)
            {
                var currentIndex = i % points.Length;
                var currentPoint = points[currentIndex];
                planPoints.Add(currentPoint);
                if (i == incommingPointIndex + points.Length)
                    //the last point cannot be outgoing for anything
                    break;

                var currentOutgoingJoins = getOutgoingJoinsFrom(currentIndex, outgoingJoins);
                foreach (var currentOutgoingJoin in currentOutgoingJoins)
                {
                    //insert connected shape
                    fillPlanBy(planPoints, currentOutgoingJoin.Item2, currentOutgoingJoin.JoinPointIndex2);
                    //plan the returning point
                    planPoints.Add(currentPoint);
                }
            }
        }

        private IEnumerable<ItemJoin> getOutgoingJoinsFrom(int currentIndex, IEnumerable<ItemJoin> outgoingJoins)
        {
            var result = new List<ItemJoin>();
            foreach (var join in outgoingJoins)
            {
                if (join.JoinPointIndex1 == currentIndex)
                    result.Add(join);
            }

            return result;
        }

        private IEnumerable<ItemJoin> findOutgoingJoins(PointProviderItem item)
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

                var geometryUV = createLinkArrow(startPointUV, endPointUV);
                var geometryXY = createLinkArrow(startPointXY, endPointXY);
                dc.DrawGeometry(null, _joinPenUV, geometryUV);
                dc.DrawGeometry(null, _joinPenXY, geometryXY);
            }
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
            if (OnWorkItemListChanged != null)
                OnWorkItemListChanged();
        }

        private void fireOnSettingsChanged()
        {
            if (OnSettingsChanged != null)
                OnSettingsChanged();
        }

        private void fireOnWorkitemClicked(WorkspaceItem item)
        {
            if (OnWorkItemClicked != null)
                OnWorkItemClicked(item);
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

        private Geometry createLinkArrow(Point p1, Point p2)
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
