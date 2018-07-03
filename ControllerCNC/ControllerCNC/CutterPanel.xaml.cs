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
using ControllerCNC.Loading;

namespace ControllerCNC
{
    public enum WorkspaceMode { Idle = 0, JoinMode, ScaffoldMode, LastMode = ScaffoldMode };

    /// <summary>
    /// Interaction logic for CutterPane.xaml
    /// </summary>
    public partial class CutterPanel : Window, ILoadProvider
    {
        internal static readonly string[] Modes =
        {
            "Join mode",
            "Scaffolding"
        };

        internal readonly Coord2DController CoordController;

        internal WorkspacePanel Workspace;

        internal readonly DriverCNC2 Cnc;

        private static string _autosaveFile = "workspace_autosave.cwspc";

        private string _workspaceFile;

        private readonly ShapeFactory _factory;

        private readonly HashSet<Control> _motionCommands = new HashSet<Control>();

        private readonly HashSet<ToggleButton> _transitionToggles = new HashSet<ToggleButton>();

        private readonly DispatcherTimer _statusTimer = new DispatcherTimer();

        private readonly DispatcherTimer _messageTimer = new DispatcherTimer();

        private readonly DispatcherTimer _autosaveTime = new DispatcherTimer();

        private readonly int _messageShowDelay = 3000;

        private int _lastRemainingSeconds = 0;

        private int _positionOffsetU = 0;

        private int _positionOffsetV = 0;

        private int _positionOffsetX = 0;

        private int _positionOffsetY = 0;

        private WorkspaceMode _currentMode = WorkspaceMode.Idle;

        private bool _addJoinsEnabled = false;

        private bool _isPlanRunning = false;

        private DateTime _planStart;

        private PointProviderItem _joinItemCandidate;

        public CutterPanel()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;
            SystemUtilities.PreventSleepMode();

            InitializeComponent();
            MessageBox.Visibility = Visibility.Hidden;

            _motionCommands.Add(Calibration);
            _motionCommands.Add(GoToZeros);
            _motionCommands.Add(AlignHeads);
            _motionCommands.Add(StartPlan);
            _motionCommands.Add(CuttingDeltaT);

            Cnc = new DriverCNC2();
            Cnc.OnConnectionStatusChange += () => Dispatcher.Invoke(refreshConnectionStatus);
            Cnc.OnHomeCalibrated += () => Dispatcher.Invoke(enableMotionCommands);

            Cnc.Initialize();

            CoordController = new Coord2DController(Cnc);

            _messageTimer.Interval = TimeSpan.FromMilliseconds(_messageShowDelay);
            _messageTimer.IsEnabled = false;
            _messageTimer.Tick += _messageTimer_Tick;
            _statusTimer.Interval = TimeSpan.FromMilliseconds(20);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
            _autosaveTime.IsEnabled = false;
            _autosaveTime.Interval = TimeSpan.FromMilliseconds(1000);
            _autosaveTime.Tick += _autosaveTime_Tick;

            KeyUp += keyUp;
            KeyDown += keyDown;
            ContextMenu = createWorkspaceMenu();

            resetWorkspace(true);

            initializeTransitionHandlers();

            _factory = new ShapeFactory(this);

            /*/
            OpenEditor_Click(null, null);
            this.Hide();
            /**/
        }

        #region Panel service

        public ReadableIdentifier UnusedVersion(ReadableIdentifier identifier)
        {
            return Workspace.UnusedVersion(identifier);
        }

        #endregion

        #region Message handling

        public void ShowError(string message, bool forceRefresh = false)
        {
            setMessageText(message, Brushes.DarkRed, Brushes.Red, forceRefresh);
        }

        public void ShowMessage(string message, bool forceRefresh = false)
        {
            setMessageText(message, Brushes.DarkOrchid, Brushes.Orchid, forceRefresh);
        }

        public void HideMessage()
        {
            _messageTimer.IsEnabled = false;
            Message.Text = "";
            MessageBox.Visibility = Visibility.Hidden;
        }

        private void setMessageText(string message, Brush borderColor, Brush color, bool forceRefresh)
        {
            Message.Text = message;
            Message.Background = color;
            Message.BorderBrush = borderColor;
            MessageBox.Visibility = Visibility.Visible;

            if (forceRefresh)
            {
                MessageBox.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }
        }

        void _messageTimer_Tick(object sender, EventArgs e)
        {
            HideMessage();
        }

        #endregion

        #region Workspace handling

        private void resetWorkspace(bool withReload)
        {
            _workspaceFile = _autosaveFile;
            if (Workspace != null)
            {
                _autosaveTime.IsEnabled = false;
            }

            Workspace = new WorkspacePanel(Configuration.MaxStepsX, Configuration.MaxStepsY);
            WorkspaceSlot.Child = Workspace;
            if (withReload)
            {
                reloadWorkspace();
            }

            Workspace.OnWorkItemListChanged += refreshItemList;
            Workspace.OnSettingsChanged += onSettingsChanged;
            Workspace.OnWorkItemClicked += onItemClicked;
            CuttingDeltaT.Value = Workspace.CuttingSpeed.ToDeltaT();
            WireLength.Text = Workspace.WireLength.ToString();
            CuttingKerf.Text = Workspace.CuttingKerf.ToString();

            refreshItemList();
            onSettingsChanged();
        }

        private void reloadWorkspace()
        {
            if (!File.Exists(_workspaceFile))
                return;

            try
            {
                Workspace.LoadFrom(_workspaceFile);
            }
            catch (Exception ex) when (
                    ex is System.Reflection.TargetInvocationException ||
                    ex is System.Runtime.Serialization.SerializationException
                )
            {
                ShowError("Saved workspace cannot be load (it's an old version).");
                if (_workspaceFile != _autosaveFile)
                {
                    _workspaceFile = _autosaveFile;
                    reloadWorkspace();
                }
                else
                {
                    var i = 0;
                    var backupFile = _workspaceFile + ".bak";
                    while (File.Exists(backupFile))
                    {
                        i += 1;
                        backupFile = _workspaceFile + "." + i + ".bak";
                    }
                    //keep old autosave
                    File.Move(_autosaveFile, backupFile);
                }
            }

            CuttingDeltaT.Value = Workspace.CuttingSpeed.ToDeltaT();
            WireLength.Text = Workspace.WireLength.ToString();
            CuttingKerf.Text = Workspace.CuttingKerf.ToString();
        }

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

            Workspace.SetJoin(_joinItemCandidate, pointProvider);
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

            foreach (var child in Workspace.Children)
            {
                var workItem = child as WorkspaceItem;
                if (workItem == null)
                    continue;

                var item = createListItem(workItem);
                foreach (var join in Workspace.GetIncomingJoins(workItem))
                {
                    var joinItem = createListItem(join);
                    WorkItemList.Items.Add(joinItem);
                }
                WorkItemList.Items.Add(item);
            }
        }

        private ContextMenu createWorkspaceMenu()
        {
            var menu = new ContextMenu();

            var centerCutPanel = new MenuItem();
            centerCutPanel.Header = "Center cut panel";
            centerCutPanel.Click += (e, s) =>
            {
                var dialog = new CenterCutPanelDialog(this);
                dialog.ShowDialog();
            };

            menu.Items.Add(centerCutPanel);

            return menu;
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
                Workspace.SetJoin(join.Item1, join.Item2);
            };
            menu.Items.Add(refreshItem);

            var deleteItem = new MenuItem();
            deleteItem.Header = "Delete";
            deleteItem.Click += (e, s) =>
            {
                Workspace.RemoveJoin(join);
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
                    Workspace.Children.Add(shapeItem.Clone(Workspace.UnusedVersion(shapeItem.Name)));
                };
                menu.Items.Add(copyItem);

                var deleteItem = new MenuItem();
                deleteItem.Header = "Delete";
                deleteItem.Click += (e, s) =>
                {
                    Workspace.Children.Remove(shapeItem);
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
                setMode(WorkspaceMode.JoinMode);
                e.Handled = true;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                setMode(WorkspaceMode.ScaffoldMode);
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
                        Workspace.RemoveJoin(join);
                    }

                    var workItem = item.Tag as WorkspaceItem;
                    if (workItem == null || workItem is EntryPoint)
                        continue;

                    Workspace.Children.Remove(workItem);
                }
            }

            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                setMode(WorkspaceMode.Idle);
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                setMode(WorkspaceMode.Idle);
            }
        }

        void _autosaveTime_Tick(object sender, EventArgs e)
        {
            _autosaveTime.IsEnabled = false;
            Workspace.SaveTo(_workspaceFile);
        }

        #endregion

        #region CNC machine status handling

        private void refreshConnectionStatus()
        {
            if (Cnc.IsConnected)
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
            var currentU = Cnc.EstimationU;
            var currentV = Cnc.EstimationV;
            var currentX = Cnc.EstimationX;
            var currentY = Cnc.EstimationY;
            var positionU = Configuration.MilimetersPerStep * (currentU - _positionOffsetU);
            var positionV = Configuration.MilimetersPerStep * (currentV - _positionOffsetV);
            var positionX = Configuration.MilimetersPerStep * (currentX - _positionOffsetX);
            var positionY = Configuration.MilimetersPerStep * (currentY - _positionOffsetY);

            PositionU.Text = positionU.ToString("0.000");
            PositionV.Text = positionV.ToString("0.000");
            PositionX.Text = positionX.ToString("0.000");
            PositionY.Text = positionY.ToString("0.000");

            positionU = Configuration.MilimetersPerStep * currentU;
            positionV = Configuration.MilimetersPerStep * currentV;
            positionX = Configuration.MilimetersPerStep * currentX;
            positionY = Configuration.MilimetersPerStep * currentY;

            var uv = new Point2Dmm(positionU, positionV);
            var xy = new Point2Dmm(positionX, positionY);

            Workspace.HeadUV.Position = uv;
            Workspace.HeadXY.Position = xy;

            if (_isPlanRunning)
            {
                var remainingTicks = Cnc.RemainingPlanTickEstimation;
                var remainingSeconds = (int)(remainingTicks / Configuration.TimerFrequency);
                if (_lastRemainingSeconds > remainingSeconds || Math.Abs(_lastRemainingSeconds - remainingSeconds) > 2)
                {
                    _lastRemainingSeconds = remainingSeconds;
                    var remainingTime = new TimeSpan(0, 0, 0, _lastRemainingSeconds);
                    var elapsedTime = new TimeSpan(0, 0, 0, (int)(DateTime.Now - _planStart).TotalSeconds);

                    if (remainingTime.TotalDays < 2)
                    {
                        ShowMessage(elapsedTime.ToString() + "/" + remainingTime.ToString());
                    }
                }
            }

            if (Cnc.CurrentState.IsHomeCalibrated)
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
            if (!Cnc.IsHomeCalibrated)
            {
                ShowError("Calibration is required!");
                return;
            }

            disableMotionCommands();
            Workspace.DisableChanges();

            var builder = new PlanBuilder();
            var point = Workspace.EntryPoint;

            var state = Cnc.PlannedState;
            var initU = point.PositionC1 - state.U;
            var initV = point.PositionC2 - state.V;
            var initX = point.PositionC1 - state.X;
            var initY = point.PositionC2 - state.Y;


            builder.AddRampedLineUVXY(initU, initV, initX, initY, Configuration.MaxPlaneAcceleration, getTransitionSpeed());
            try
            {
                Workspace.BuildPlan(builder);
            }
            catch (PlanningException ex)
            {
                planCompleted();
                ShowError(ex.Message);
                return;
            }
            //builder.AddRampedLineUVXY(-initU, -initV, -initX, -initY, Constants.MaxPlaneAcceleration, getTransitionSpeed());

            var plan = builder.Build();

            if (!Cnc.SEND(plan))
            {
                planCompleted();
                ShowError("Plan overflows the workspace!");
                return;
            }

            Cnc.OnInstructionQueueIsComplete += planCompleted;
            _planStart = DateTime.Now;
            _isPlanRunning = true;

            if (!plan.Any())
                planCompleted();
        }

        private void planCompleted()
        {
            _isPlanRunning = false;
            _lastRemainingSeconds = 0;
            Cnc.OnInstructionQueueIsComplete -= planCompleted;
            Workspace.EnableChanges();
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
            CuttingDeltaT.Value = Workspace.CuttingSpeed.ToDeltaT();
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

            refreshTransitionSpeed();
            CoordController.SetPlanes(MoveUV.IsChecked.Value, MoveXY.IsChecked.Value);
            CoordController.SetMovement(dirX, dirY);
        }

        private void refreshTransitionSpeed()
        {
            if (CoordController == null)
                return;

            CoordController.SetSpeed(getTransitionSpeed().ToDeltaT());
        }

        private Speed getTransitionSpeed()
        {
            if (MoveByCuttingSpeed.IsChecked.Value)
            {
                return Workspace.CuttingSpeed;
            }
            else
            {
                return Configuration.MaxPlaneSpeed;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AlignHeads_Click(object sender, RoutedEventArgs e)
        {
            var state = Cnc.PlannedState;
            var xSteps = state.X - state.U;
            var ySteps = state.Y - state.V;

            var builder = new PlanBuilder();
            builder.AddRampedLineXY(-xSteps, -ySteps, Configuration.MaxPlaneAcceleration, Configuration.MaxPlaneSpeed);
            Cnc.SEND(builder.Build());
        }

        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            disableMotionCommands();
            Cnc.SEND(new HomingInstruction());
        }

        private void ResetCoordinates_Click(object sender, RoutedEventArgs e)
        {
            var state = Cnc.CurrentState;
            _positionOffsetU = state.U;
            _positionOffsetV = state.V;
            _positionOffsetX = state.X;
            _positionOffsetY = state.Y;
        }

        private void GoToZeros_Click(object sender, RoutedEventArgs e)
        {
            var state = Cnc.PlannedState;
            var stepsU = _positionOffsetU - state.U;
            var stepsV = _positionOffsetV - state.V;
            var stepsX = _positionOffsetX - state.X;
            var stepsY = _positionOffsetY - state.Y;

            var planner = new PlanBuilder();
            planner.AddRampedLineUVXY(stepsU, stepsV, stepsX, stepsY, Configuration.MaxPlaneAcceleration, Configuration.MaxPlaneSpeed);
            Cnc.SEND(planner.Build());
        }

        private void StartPlan_Click(object sender, RoutedEventArgs e)
        {
            executePlan();
        }

        private void AddShape_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All supported files|*.jpeg;*.jpg;*.png;*.bmp;*.dat;*.cor;*.4dcor;*.slice_cut;*.line_path|Image files|*.jpeg;*.jpg;*.png;*.bmp|Coordinate files|*.dat;*.cor;*.4dcor|Slice files|*.slice_cut|Line path files|*.line_path";

            if (dlg.ShowDialog().Value)
            {
                var filename = dlg.FileName;
                var shape = _factory.Load(filename);
                if (shape != null)
                    Workspace.Children.Add(shape);
            }
        }

        private void RefreshJoins_Click(object sender, RoutedEventArgs e)
        {
            Workspace.RefreshJoins();
        }

        private void setMode(WorkspaceMode mode)
        {
            if (_currentMode == mode)
                //nothing to change
                return;

            if (mode == WorkspaceMode.Idle)
            {
                _currentMode = WorkspaceMode.LastMode;
                ModeSwitch.IsChecked = false;
                return;
            }

            if (!ModeSwitch.IsChecked.Value)
            {
                _currentMode = mode;
                ModeSwitch.IsChecked = true;
                return;
            }

            _currentMode = mode - 1;
            ModeSwitch.IsChecked = false;
        }

        private void ModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WorkspaceMode.Idle)
                //idle mode is neve checked 
                _currentMode = WorkspaceMode.JoinMode;

            switch (_currentMode)
            {
                case WorkspaceMode.JoinMode:
                    _addJoinsEnabled = true;
                    _joinItemCandidate = null;

                    Workspace.ScaffoldModeEnabled = false;
                    break;
                case WorkspaceMode.ScaffoldMode:
                    Workspace.ScaffoldModeEnabled = true;

                    _addJoinsEnabled = false;
                    _joinItemCandidate = null;
                    break;

                default:
                    throw new NotImplementedException("Workspace mode");
            }

            ModeSwitch.Content = Modes[(int)_currentMode - 1];
        }

        private void ModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            _addJoinsEnabled = false;
            if (_joinItemCandidate != null)
                _joinItemCandidate.IsHighlighted = false;
            _joinItemCandidate = null;
            Workspace.ScaffoldModeEnabled = false;

            var nextMode = (int)(_currentMode + 1);
            if (nextMode >= (int)WorkspaceMode.LastMode)
                nextMode = 0;

            _currentMode = (WorkspaceMode)nextMode;
            if (_currentMode == WorkspaceMode.Idle)
            {
                ModeSwitch.Content = Modes[0];
            }
            else
            {
                //recheck the switch
                ModeSwitch.IsChecked = true;
            }
        }

        private void CuttingDeltaT_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var deltaT = (int)Math.Round(CuttingDeltaT.Value);
            refreshTransitionSpeed();

            if (Workspace != null)
                Workspace.CuttingSpeed = Speed.FromDeltaT(deltaT);

            var speed = Configuration.MilimetersPerStep * Configuration.TimerFrequency / deltaT;
            CuttingSpeed.Text = string.Format("{0:0.000}mm/s", speed);
        }

        private void CuttingKerf_TextChanged(object sender, TextChangedEventArgs e)
        {
            double kerf;
            double.TryParse(CuttingKerf.Text, out kerf);
            if (Workspace != null)
                Workspace.CuttingKerf = kerf;
        }

        private void WireLength_TextChanged(object sender, TextChangedEventArgs e)
        {
            double length;
            double.TryParse(WireLength.Text, out length);

            if (Workspace != null)
                Workspace.WireLength = length;
        }

        private void LoadPlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Cutter workspace|*.cwspc";
            if (dlg.ShowDialog() == true)
            {
                _workspaceFile = dlg.FileName;
                reloadWorkspace();
            }
        }

        private void SavePlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Cutter workspace|*.cwspc";
            if (dlg.ShowDialog() == true)
            {
                _workspaceFile = dlg.FileName;
                Workspace.SaveTo(_workspaceFile);
            }
        }

        private void OpenEditor_Click(object sender, RoutedEventArgs e)
        {
            var editor = new ShapeEditor.EditorWindow();
            editor.Show();
        }

        private void NewPlan_Click(object sender, RoutedEventArgs e)
        {
            resetWorkspace(false);
        }

        private void MessageBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HideMessage();
        }

        public bool UsePartJoins()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
