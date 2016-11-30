using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Windows.Controls.Primitives;

using System.Runtime.Serialization.Formatters.Binary;

using System.IO;
using System.Threading;
using System.Windows.Threading;

using System.Windows.Media.Effects;

using ControllerCNC.GUI;
using ControllerCNC.Demos;
using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC
{
    /// <summary>
    /// Interaction logic for CutterPane.xaml
    /// </summary>
    public partial class CutterPanel : Window
    {
        private readonly string _autosaveFile = "workspace_autosave.bin";

        private readonly DriverCNC _cnc;

        private readonly Coord2DController _coordController;

        private readonly HashSet<Control> _motionCommands = new HashSet<Control>();

        private readonly HashSet<ToggleButton> _transitionToggles = new HashSet<ToggleButton>();

        private readonly DispatcherTimer _statusTimer = new DispatcherTimer();

        private readonly DispatcherTimer _autosaveTime = new DispatcherTimer();

        private readonly WorkspacePanel _workspace;

        private readonly WorkspaceItem _uvHead = new HeadCNC(Colors.Blue, true);

        private readonly WorkspaceItem _xyHead = new HeadCNC(Colors.Red, false);

        private int _positionOffsetU = 0;

        private int _positionOffsetV = 0;

        private int _positionOffsetX = 0;

        private int _positionOffsetY = 0;

        private bool _addJoinsEnabled = false;

        private PointProviderItem _joinItemCandidate;

        public CutterPanel()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            InitializeComponent();

            _motionCommands.Add(Calibration);
            _motionCommands.Add(GoToZeros);
            _motionCommands.Add(AlignHeads);
            _motionCommands.Add(StartPlan);
            _motionCommands.Add(CuttingDeltaT);

            _cnc = new DriverCNC();
            _cnc.OnConnectionStatusChange += () => Dispatcher.Invoke(refreshConnectionStatus);
            _cnc.OnHomingEnded += () => Dispatcher.Invoke(enableMotionCommands);

            _cnc.Initialize();

            _coordController = new Coord2DController(_cnc);


            _statusTimer.Interval = TimeSpan.FromMilliseconds(20);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
            _autosaveTime.IsEnabled = false;
            _autosaveTime.Interval = TimeSpan.FromMilliseconds(1000);
            _autosaveTime.Tick += _autosaveTime_Tick;

            KeyUp += keyUp;
            KeyDown += keyDown;


            //setup workspace
            _workspace = new WorkspacePanel(Constants.MaxStepsX, Constants.MaxStepsY);
            WorkspaceSlot.Child = _workspace;

            if (File.Exists(_autosaveFile))
                _workspace.LoadFrom(_autosaveFile);

            _workspace.Children.Add(_xyHead);
            _workspace.Children.Add(_uvHead);
            /*/
            var points = ShapeDrawing.CircleToSquare();
            var shape = new ShapeItem4D(new ReadableIdentifier("Test4D"), points.As4Df());
            shape.MetricThickness = 100;
            shape.MetricWidth = 50;
            _workspace.Children.Add(shape);/**/
            _workspace.OnWorkItemListChanged += refreshItemList;
            _workspace.OnSettingsChanged += onSettingsChanged;
            _workspace.OnWorkItemClicked += onItemClicked;

            refreshItemList();
            onSettingsChanged();

            initializeTransitionHandlers();
        }

        #region Workspace handling

        private void onItemClicked(WorkspaceItem item)
        {
            if (!_addJoinsEnabled || !(item is PointProviderItem))
                //nothing to do
                return;

            if (_joinItemCandidate != null)
                _joinItemCandidate.IsHighlighted = false;

            if (item == _joinItemCandidate)
            {
                //selection was discarded
                _joinItemCandidate = null;
                return;
            }

            var pointProvider = item as PointProviderItem;
            pointProvider.IsHighlighted = true;
            if (_joinItemCandidate == null || pointProvider is EntryPoint)
            {
                //first item is set (entry point has to be first)
                _joinItemCandidate = pointProvider;
                return;
            }

            _workspace.SetJoin(_joinItemCandidate, pointProvider);
            _joinItemCandidate = pointProvider;
        }

        private void onSettingsChanged()
        {
            //reset autosave timer
            _autosaveTime.Stop();
            _autosaveTime.Start();
        }

        private void refreshItemList()
        {
            WorkItemList.Items.Clear();

            foreach (var child in _workspace.Children)
            {
                var workItem = child as WorkspaceItem;
                if (workItem == null || workItem is HeadCNC)
                    continue;

                var item = createListItem(workItem);
                foreach (var join in _workspace.GetIncomingJoins(workItem))
                {
                    var joinItem = createListItem(join);
                    WorkItemList.Items.Add(joinItem);
                }
                WorkItemList.Items.Add(item);
            }
        }

        private ListBoxItem createListItem(ItemJoin join)
        {
            var item = new ListBoxItem();
            item.Content = join.Item1.Name + " --> " + join.Item2.Name;
            item.Tag = join;
            item.Foreground = Brushes.Red;

            var menu = new ContextMenu();

            var refreshItem = new MenuItem();
            refreshItem.Header = "Refresh";
            refreshItem.Click += (e, s) =>
            {
                _workspace.SetJoin(join.Item1, join.Item2);
            };
            menu.Items.Add(refreshItem);

            var deleteItem = new MenuItem();
            deleteItem.Header = "Delete";
            deleteItem.Click += (e, s) =>
            {
                _workspace.RemoveJoin(join);
            };
            menu.Items.Add(deleteItem);

            item.ContextMenu = menu;



            return item;
        }

        private ListBoxItem createListItem(WorkspaceItem workItem)
        {
            var item = new ListBoxItem();
            item.Content = workItem.Name;
            item.Tag = workItem;

            var shapeItem = workItem as ShapeItem;
            if (shapeItem != null)
            {
                var menu = new ContextMenu();

                var copyItem = new MenuItem();
                copyItem.Header = "Copy";
                copyItem.Click += (e, s) =>
                {
                    _workspace.Children.Add(shapeItem.Clone(_workspace.UnusedVersion(shapeItem.Name)));
                };
                menu.Items.Add(copyItem);

                var deleteItem = new MenuItem();
                deleteItem.Header = "Delete";
                deleteItem.Click += (e, s) =>
                {
                    _workspace.Children.Remove(shapeItem);
                };
                menu.Items.Add(deleteItem);

                item.ContextMenu = menu;
            }
            return item;
        }

        void keyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                JoinMode.IsChecked = true;
            }
        }

        void keyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                //delete all selected items
                var itemsCopy = WorkItemList.SelectedItems.OfType<object>().ToArray();
                foreach (ListBoxItem item in itemsCopy)
                {
                    var join = item.Tag as ItemJoin;
                    if (join != null)
                    {
                        _workspace.RemoveJoin(join);
                    }

                    var workItem = item.Tag as WorkspaceItem;
                    if (workItem == null || workItem is EntryPoint)
                        continue;

                    _workspace.Children.Remove(workItem);
                }
            }

            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                JoinMode.IsChecked = false;
            }
        }


        void _autosaveTime_Tick(object sender, EventArgs e)
        {
            _autosaveTime.IsEnabled = false;
            _workspace.SaveTo(_autosaveFile);
        }

        #endregion

        #region CNC machine status handling

        private void refreshConnectionStatus()
        {
            if (_cnc.IsConnected)
            {
                ConnectionStatus.Text = "Online";
                ConnectionStatus.Background = Brushes.Green;
            }
            else
            {
                ConnectionStatus.Text = "Offline";
                ConnectionStatus.Background = Brushes.Red;
            }
        }


        void _statusTimer_Tick(object sender, EventArgs e)
        {
            var currentU = _cnc.EstimationU;
            var currentV = _cnc.EstimationV;
            var currentX = _cnc.EstimationX;
            var currentY = _cnc.EstimationY;
            var positionU = Constants.MilimetersPerStep * (currentU - _positionOffsetU);
            var positionV = Constants.MilimetersPerStep * (currentV - _positionOffsetV);
            var positionX = Constants.MilimetersPerStep * (currentX - _positionOffsetX);
            var positionY = Constants.MilimetersPerStep * (currentY - _positionOffsetY);

            PositionU.Text = positionU.ToString("0.000");
            PositionV.Text = positionV.ToString("0.000");
            PositionX.Text = positionX.ToString("0.000");
            PositionY.Text = positionY.ToString("0.000");

            _uvHead.PositionC1 = currentU;
            _uvHead.PositionC2 = currentV;

            _xyHead.PositionC1 = currentX;
            _xyHead.PositionC2 = currentY;

            if (_cnc.CompletedState.IsHomeCalibrated)
            {
                Calibration.Foreground = Brushes.Black;
            }
            else
            {
                var blinkFrequency = 1000;
                if (DateTime.Now.Millisecond % blinkFrequency > blinkFrequency / 2)
                {
                    Calibration.Foreground = Brushes.Red;
                }
                else
                {
                    Calibration.Foreground = Brushes.Green;
                }
            }
        }


        #endregion

        #region Plan execution

        private void executePlan()
        {
            disableMotionCommands();
            _workspace.DisableChanges();

            var builder = new PlanBuilder();
            var point = _workspace.EntryPoint;

            var state = _cnc.PlannedState;
            var initU = point.PositionC1 - state.U;
            var initV = point.PositionC2 - state.V;
            var initX = point.PositionC1 - state.X;
            var initY = point.PositionC2 - state.Y;


            builder.AddRampedLineUVXY(initU, initV, initX, initY, Constants.MaxPlaneAcceleration, getTransitionSpeed());
            _workspace.BuildPlan(builder);
            //builder.AddRampedLineUVXY(-initU, -initV, -initX, -initY, Constants.MaxPlaneAcceleration, getTransitionSpeed());

            var plan = builder.Build();
            if (!_cnc.SEND(plan))
                throw new NotSupportedException("Invalid plan");

            _cnc.OnInstructionQueueIsComplete += planCompleted;
        }

        private void planCompleted()
        {
            _cnc.OnInstructionQueueIsComplete -= planCompleted;
            _workspace.EnableChanges();
            enableMotionCommands();
        }

        #endregion

        #region Controls implementation

        private void enableMotionCommands()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var command in _motionCommands)
                {
                    command.IsEnabled = true;
                }
            });
        }

        private void disableMotionCommands()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var command in _motionCommands)
                {
                    command.IsEnabled = false;
                }
            });
        }

        private void initializeTransitionHandlers()
        {
            initializeTransitionHandlers(UpB, 0, -1);
            initializeTransitionHandlers(BottomB, 0, 1);
            initializeTransitionHandlers(LeftB, -1, 0);
            initializeTransitionHandlers(RightB, 1, 0);

            initializeTransitionHandlers(LeftUpB, -1, -1);
            initializeTransitionHandlers(UpRightB, 1, -1);
            initializeTransitionHandlers(LeftBottomB, -1, 1);
            initializeTransitionHandlers(BottomRightB, 1, 1);

            HoldMovement.Unchecked += (s, e) => setTransition(null, 0, 0);
            CuttingDeltaT.Value = _workspace.CuttingSpeed.ToDeltaT();
            MoveByCuttingSpeed.Checked += (s, e) => refreshTransitionSpeed();
            MoveByCuttingSpeed.Unchecked += (s, e) => refreshTransitionSpeed();
            refreshTransitionSpeed();
        }

        private void initializeTransitionHandlers(ToggleButton button, int dirX, int dirY)
        {
            button.PreviewMouseDown += (s, e) => setTransition(button, dirX, dirY);
            button.Checked += (s, e) =>
            {
                if (!HoldMovement.IsChecked.Value)
                {
                    button.IsChecked = false;
                }
            };
            button.PreviewMouseUp += (s, e) =>
            {
                if (!HoldMovement.IsChecked.Value)
                {
                    setTransition(null, 0, 0);
                }
            };
            button.Unchecked += (s, e) => setTransition(null, 0, 0);
            _motionCommands.Add(button);
            _transitionToggles.Add(button);
        }

        private void setTransition(ToggleButton button, int dirX, int dirY)
        {
            foreach (var buttonToDisable in _transitionToggles)
            {
                if (buttonToDisable == button)
                    continue;

                buttonToDisable.IsChecked = false;
            }

            _coordController.SetPlanes(MoveUV.IsChecked.Value, MoveXY.IsChecked.Value);
            _coordController.SetMovement(dirX, dirY);
        }

        private void refreshTransitionSpeed()
        {
            if (_coordController == null)
                return;

            _coordController.SetSpeed(getTransitionSpeed().ToDeltaT());
        }

        private Speed getTransitionSpeed()
        {
            if (MoveByCuttingSpeed.IsChecked.Value)
            {
                return _workspace.CuttingSpeed;
            }
            else
            {
                return Constants.MaxPlaneSpeed;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AlignHeads_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.PlannedState;
            var xSteps = state.X - state.U;
            var ySteps = state.Y - state.V;

            var builder = new PlanBuilder();
            builder.AddRampedLineXY(-xSteps, -ySteps, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _cnc.SEND(builder.Build());
        }

        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            disableMotionCommands();
            _cnc.SEND(new HomingInstruction());
        }

        private void ResetCoordinates_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.CompletedState;
            _positionOffsetU = state.U;
            _positionOffsetV = state.V;
            _positionOffsetX = state.X;
            _positionOffsetY = state.Y;
        }

        private void GoToZeros_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.PlannedState;
            var stepsU = _positionOffsetU - state.U;
            var stepsV = _positionOffsetV - state.V;
            var stepsX = _positionOffsetX - state.X;
            var stepsY = _positionOffsetY - state.Y;

            var planner = new PlanBuilder();
            planner.AddRampedLineUVXY(stepsU, stepsV, stepsX, stepsY, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _cnc.SEND(planner.Build());
        }

        private void StartPlan_Click(object sender, RoutedEventArgs e)
        {
            executePlan();
        }

        private void AddShape_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All supported files|*.jpeg;*.jpg;*.png;*.bmp;*.cor;*.4dcor|Image files|*.jpeg;*.jpg;*.png;*.bmp|Coordinate files|*.cor;*.4dcor";

            if (dlg.ShowDialog().Value)
            {
                string filename = dlg.FileName;
                var extension = System.IO.Path.GetExtension(filename);
                var name = System.IO.Path.GetFileNameWithoutExtension(filename);
                var identifier = _workspace.UnusedVersion(new ReadableIdentifier(name));

                IEnumerable<Point2Dmm> coordinates;
                switch (extension.ToLower())
                {
                    case ".4dcor":
                        {
                            var formatter = new BinaryFormatter();
                            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                var definition = (ShapeDefinition4D)formatter.Deserialize(stream);
                                var shape = new ShapeItem4D(identifier, definition.Points);
                                shape.MetricThickness = definition.Thickness;
                                shape.SetOriginalSize();
                                _workspace.Children.Add(shape);
                            }
                            break;
                        }
                    case ".cor":
                        throw new NotImplementedException();
                    default:
                        {
                            var interpolator = new ImageInterpolator(filename);
                            coordinates = interpolator.InterpolateCoordinates();

                            var shape = new ShapeItem2D(identifier, coordinates);
                            shape.MetricWidth = 50;
                            _workspace.Children.Add(shape);
                            break;
                        }
                }


            }
        }

        private void RefreshJoins_Click(object sender, RoutedEventArgs e)
        {
            _workspace.RefreshJoins();
        }

        private void JoinMode_Checked(object sender, RoutedEventArgs e)
        {
            _addJoinsEnabled = true;
            _joinItemCandidate = null;
        }

        private void JoinMode_Unchecked(object sender, RoutedEventArgs e)
        {
            _addJoinsEnabled = false;
            if (_joinItemCandidate != null)
                _joinItemCandidate.IsHighlighted = false;
            _joinItemCandidate = null;
        }

        private void CuttingDeltaT_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var deltaT = (int)Math.Round(CuttingDeltaT.Value);
            refreshTransitionSpeed();

            if (_workspace != null)
                _workspace.CuttingSpeed = Speed.FromDeltaT(deltaT);

            var speed = Constants.MilimetersPerStep * Constants.TimerFrequency / deltaT;
            CuttingSpeed.Text = string.Format("{0:0.000}mm/s", speed);
        }

        #endregion
    }
}
